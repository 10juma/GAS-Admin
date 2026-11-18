using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Comercial.Catalogo;

[Authorize(Policy = Permisos.Comercial.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;

    public IndexModel(GasDbContext db) => _db = db;

    /// <summary>Filtro por categoría.</summary>
    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    [BindProperty]
    public ServicioComercialInput Input { get; set; } = new();

    public List<ServicioComercial> Servicios { get; private set; } = [];
    public Dictionary<CategoriaComercial, int> PorCategoria { get; private set; } = [];
    public int Total { get; private set; }
    public bool PuedeGestionar => User.TienePermiso(Permisos.Comercial.Gestionar);
    public ServicioComercialModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_ServicioComercialModal", await ModalAsync(null, new ServicioComercialInput()));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var s = await _db.ServiciosComerciales.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        return Partial("_ServicioComercialModal", await ModalAsync(s, ServicioComercialInput.Desde(s)));
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(null);
        if (ModelState.IsValid)
        {
            var s = new ServicioComercial();
            Input.AplicarA(s);
            _db.ServiciosComerciales.Add(s);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"{s.Nombre} agregado al catálogo.");
            return Volver();
        }
        await CargarAsync();
        Modal = await ModalAsync(null, Input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var s = await _db.ServiciosComerciales.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        await ValidarAsync(id);
        if (ModelState.IsValid)
        {
            Input.AplicarA(s);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            if (cambios) this.ToastSuccess($"Cambios de {s.Nombre} guardados. Las cotizaciones anteriores no cambian.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return Volver();
        }
        await CargarAsync();
        Modal = await ModalAsync(s, Input);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var s = await _db.ServiciosComerciales.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (await _db.CotizacionLineas.AnyAsync(l => l.ServicioComercialId == id))
        {
            this.ToastWarning($"{s.Nombre} ya se usó en cotizaciones. Márcalo como Inactivo para dejar de ofrecerlo.");
            return Volver();
        }
        _db.ServiciosComerciales.Remove(s);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"{s.Nombre} eliminado del catálogo.");
        return Volver();
    }

    private IActionResult Volver() => RedirectToPage("Index", new { estado = Opciones.Limpio(Estado), q = Opciones.Limpio(Q) });

    private async Task ValidarAsync(int? id)
    {
        var nombre = Input.Nombre?.Trim() ?? "";
        if (nombre.Length > 0 && await _db.ServiciosComerciales.AnyAsync(s => s.Nombre == nombre && s.Id != id))
        {
            ModelState.AddModelError(string.Empty, $"Ya existe en el catálogo un servicio llamado {nombre}.");
        }
        if (!ServicioComercialInput.Monedas.Contains(Input.Moneda))
        {
            ModelState.AddModelError(string.Empty, "Elige una moneda válida.");
        }
        if (Input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == Input.ProductoId))
        {
            Input.ProductoId = null;
        }
    }

    private async Task<ServicioComercialModalVm> ModalAsync(ServicioComercial? s, ServicioComercialInput input) => new()
    {
        Id = s?.Id,
        Input = input,
        Productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync(),
        PuedeGestionar = PuedeGestionar,
        Cotizaciones = s is null ? 0 : await _db.CotizacionLineas.Where(l => l.ServicioComercialId == s.Id).Select(l => l.CotizacionId).Distinct().CountAsync(),
        CreadoUtc = s?.CreadoUtc,
        CreadoPor = s?.CreadoPor,
        ModificadoUtc = s?.ModificadoUtc,
        ModificadoPor = s?.ModificadoPor,
        Filtro = Estado,
        Q = Q
    };

    private async Task CargarAsync()
    {
        var todos = await _db.ServiciosComerciales.AsNoTracking().Include(s => s.Producto).ToListAsync();
        Total = todos.Count;
        PorCategoria = todos.GroupBy(s => s.Categoria).ToDictionary(g => g.Key, g => g.Count());

        IEnumerable<ServicioComercial> filtrados = todos;
        if (Enum.TryParse<CategoriaComercial>(Estado, out var categoria)) filtrados = filtrados.Where(s => s.Categoria == categoria);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            filtrados = filtrados.Where(s => s.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)
                                             || (s.Descripcion?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        Servicios = filtrados.OrderBy(s => s.Estado).ThenBy(s => s.Categoria).ThenBy(s => s.Nombre).ToList();
    }
}
