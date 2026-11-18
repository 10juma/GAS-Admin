using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.ServiciosExternos;

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ContadoresMenu contadores)
    {
        _db = db;
        _contadores = contadores;
    }

    /// <summary>Filtro por categoría.</summary>
    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    [BindProperty]
    public ServicioInput Input { get; set; } = new();

    public sealed record Fila(ServicioExterno Servicio, List<string> Productos, string? Responsable, int? DiasParaRenovar);

    public List<Fila> Servicios { get; private set; } = [];
    public Dictionary<CategoriaServicio, int> PorCategoria { get; private set; } = [];
    public int Total { get; private set; }
    public int Activos { get; private set; }
    public int RenovacionesProximas { get; private set; }
    public int SinCosto { get; private set; }
    /// <summary>Costo mensual estimado por moneda (servicios no cancelados con costo fijo).</summary>
    public Dictionary<string, decimal> CostoMensual { get; private set; } = [];
    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);
    public ServicioModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_ServicioModal", await ModalAsync(null, new ServicioInput()));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var servicio = await _db.ServiciosExternos.AsNoTracking().Include(s => s.Productos).FirstOrDefaultAsync(s => s.Id == id);
        if (servicio is null) return NotFound();
        return Partial("_ServicioModal", await ModalAsync(servicio, ServicioInput.Desde(servicio)));
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(null);

        if (ModelState.IsValid)
        {
            var servicio = new ServicioExterno();
            Input.AplicarA(servicio);
            await _db.Productos.LoadAsync(); // nombres para la auditoría
            SincronizarProductos(servicio);
            _db.ServiciosExternos.Add(servicio);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Servicio {servicio.Nombre} registrado.");
            return Volver();
        }

        await CargarAsync();
        Modal = await ModalAsync(null, Input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var servicio = await _db.ServiciosExternos.Include(s => s.Productos).FirstOrDefaultAsync(s => s.Id == id);
        if (servicio is null) return NotFound();
        await ValidarAsync(id);

        if (ModelState.IsValid)
        {
            Input.AplicarA(servicio);
            await _db.Productos.LoadAsync();
            SincronizarProductos(servicio);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            if (cambios) this.ToastSuccess($"Cambios de {servicio.Nombre} guardados.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return Volver();
        }

        await CargarAsync();
        Modal = await ModalAsync(servicio, Input);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var servicio = await _db.ServiciosExternos.Include(s => s.Productos).FirstOrDefaultAsync(s => s.Id == id);
        if (servicio is null) return NotFound();

        _db.ServiciosExternos.Remove(servicio);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Servicio {servicio.Nombre} eliminado.");
        return Volver();
    }

    // ---------- Apoyo ----------

    private IActionResult Volver()
    {
        _contadores.Invalidar();
        return RedirectToPage("Index", new { estado = Opciones.Limpio(Estado), q = Opciones.Limpio(Q) });
    }

    private void SincronizarProductos(ServicioExterno servicio)
    {
        var nuevos = Input.Productos.Distinct().ToHashSet();
        servicio.Productos.RemoveAll(p => !nuevos.Contains(p.ProductoId));
        foreach (var id in nuevos.Where(id => servicio.Productos.All(p => p.ProductoId != id)))
        {
            servicio.Productos.Add(new ServicioProducto { ProductoId = id });
        }
    }

    private async Task ValidarAsync(int? id)
    {
        var nombre = Input.Nombre?.Trim() ?? "";
        if (nombre.Length > 0 && await _db.ServiciosExternos.AnyAsync(s => s.Nombre == nombre && s.Id != id))
        {
            ModelState.AddModelError(string.Empty, $"Ya existe un servicio llamado {nombre}.");
        }
        if (!ServicioInput.Monedas.Contains(Input.Moneda))
        {
            ModelState.AddModelError(string.Empty, "Elige una moneda válida.");
        }
        if (DetectorSecretos.PareceSecreto(Input.ReferenciaCredenciales))
        {
            ModelState.AddModelError(string.Empty,
                "La referencia parece una contraseña o API key. Guarda solo dónde está (por ejemplo: 1Password › GAS › Stripe).");
            // No volver a mostrar el posible secreto en el formulario.
            Input.ReferenciaCredenciales = null;
        }
        if (Input.Costo is not null && Input.Periodicidad == PeriodicidadCobro.Gratuito)
        {
            Input.Costo = null;
        }

        var validos = (await _db.Productos.Select(p => p.Id).ToListAsync()).ToHashSet();
        Input.Productos = Input.Productos.Where(validos.Contains).ToList();
    }

    private async Task<ServicioModalVm> ModalAsync(ServicioExterno? servicio, ServicioInput input) => new()
    {
        Id = servicio?.Id,
        Input = input,
        ProductosDisponibles = await _db.Productos.AsNoTracking().OrderBy(p => p.Estado).ThenBy(p => p.Nombre).ToListAsync(),
        Responsables = await Opciones.ResponsablesAsync(_db, servicio?.ResponsableId),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = servicio?.CreadoUtc,
        CreadoPor = servicio?.CreadoPor,
        ModificadoUtc = servicio?.ModificadoUtc,
        ModificadoPor = servicio?.ModificadoPor,
        Filtro = Estado,
        Q = Q
    };

    private async Task CargarAsync()
    {
        var todos = await _db.ServiciosExternos.AsNoTracking()
            .Include(s => s.Productos).ThenInclude(p => p.Producto)
            .Include(s => s.Responsable)
            .ToListAsync();

        var hoy = DateOnly.FromDateTime(Fechas.ALocal(DateTime.UtcNow));
        int? Dias(ServicioExterno s) => s.ProximaRenovacion is null ? null : s.ProximaRenovacion.Value.DayNumber - hoy.DayNumber;

        Total = todos.Count;
        Activos = todos.Count(s => s.Estado != EstadoServicio.Cancelado);
        RenovacionesProximas = todos.Count(s => s.Estado != EstadoServicio.Cancelado && Dias(s) is <= 30);
        SinCosto = todos.Count(s => s.Estado != EstadoServicio.Cancelado && s.Costo is null
                                    && s.Periodicidad is not (PeriodicidadCobro.Gratuito or PeriodicidadCobro.PorUso));
        CostoMensual = todos.Where(s => s.CostoMensual is not null)
            .GroupBy(s => s.Moneda)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.CostoMensual!.Value));
        PorCategoria = todos.GroupBy(s => s.Categoria).ToDictionary(g => g.Key, g => g.Count());

        IEnumerable<ServicioExterno> filtrados = todos;
        if (Enum.TryParse<CategoriaServicio>(Estado, out var categoria)) filtrados = filtrados.Where(s => s.Categoria == categoria);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            filtrados = filtrados.Where(s => s.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)
                                             || (s.Descripcion?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                                             || (s.CuentaAsociada?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Servicios = filtrados
            .OrderBy(s => s.Estado == EstadoServicio.Cancelado)
            .ThenBy(s => s.ProximaRenovacion ?? DateOnly.MaxValue)
            .ThenBy(s => s.Nombre)
            .Select(s => new Fila(
                s,
                s.Productos.Select(p => p.Producto.Nombre).OrderBy(n => n).ToList(),
                s.Responsable is null ? null : s.Responsable.NombreVisible,
                Dias(s)))
            .ToList();
    }
}
