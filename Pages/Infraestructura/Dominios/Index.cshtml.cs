using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Finanzas;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Dominios;

public class DominioInput
{
    [Required(ErrorMessage = "Ingresa el dominio.")]
    [StringLength(253, ErrorMessage = "El dominio es demasiado largo.")]
    public string Nombre { get; set; } = string.Empty;
    public int? ProductoId { get; set; }
    public int? RegistradorId { get; set; }
    [StringLength(150, ErrorMessage = "El DNS no puede exceder 150 caracteres.")]
    public string? Dns { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    [Range(1, 10, ErrorMessage = "Los años por renovación deben estar entre 1 y 10.")]
    public int AniosRenovacion { get; set; } = 1;
    public bool RenovacionAutomatica { get; set; }
    [Range(0, 999_999, ErrorMessage = "El costo debe ser positivo.")]
    public decimal? Costo { get; set; }
    public string Moneda { get; set; } = "USD";
    public DateOnly? SslVence { get; set; }
    [StringLength(100, ErrorMessage = "El emisor no puede exceder 100 caracteres.")]
    public string? SslEmisor { get; set; }
    public EstadoDominio Estado { get; set; } = EstadoDominio.Activo;
    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }
}

public class DominioModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public DominioInput Input { get; init; } = new();
    public int Cargos { get; init; }
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Servicios { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
}

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public partial class IndexModel : PageModel
{
    private const int Horizonte = 60;
    private readonly GasDbContext _db;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ContadoresMenu contadores)
    {
        _db = db;
        _contadores = contadores;
    }

    /// <summary>activos (por defecto), porvencer o todos.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);
    public DateOnly Hoy { get; } = ServicioFinanzas.Hoy;

    public sealed record Fila(Dominio Dominio, string? Producto, string? Registrador);

    public List<Fila> Dominios { get; private set; } = [];
    public int Activos { get; private set; }
    public int PorVencer { get; private set; }
    public int Total { get; private set; }
    public decimal CostoAnualUsd { get; private set; }
    public decimal CostoAnualMxn { get; private set; }
    public DominioModalVm? Modal { get; private set; }

    public bool Proximo(DateOnly? f) => f is not null && f.Value <= Hoy.AddDays(Horizonte);

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        var godaddy = await _db.ServiciosExternos.Where(s => s.Nombre == "GoDaddy").Select(s => (int?)s.Id).FirstOrDefaultAsync();
        return Partial("_DominioModal", await ModalAsync(null, new DominioInput { RegistradorId = godaddy }));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var d = await _db.Dominios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        return Partial("_DominioModal", await ModalAsync(d, new DominioInput
        {
            Nombre = d.Nombre, ProductoId = d.ProductoId, RegistradorId = d.RegistradorId, Dns = d.Dns, FechaVencimiento = d.FechaVencimiento,
            AniosRenovacion = d.AniosRenovacion, RenovacionAutomatica = d.RenovacionAutomatica, Costo = d.Costo, Moneda = d.Moneda,
            SslVence = d.SslVence, SslEmisor = d.SslEmisor, Estado = d.Estado, Notas = d.Notas
        }));
    }

    public async Task<IActionResult> OnPostGuardarAsync(int? id, [Bind(Prefix = "Input")] DominioInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var d = id is null ? new Dominio() : await _db.Dominios.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();

        var nombre = Normalizar(input.Nombre);
        if (!NombreValido().IsMatch(nombre)) ModelState.AddModelError(string.Empty, "Escribe solo el dominio, por ejemplo revendo.mx (sin https:// ni rutas).");
        else if (await _db.Dominios.AnyAsync(x => x.Id != id && x.Nombre == nombre)) ModelState.AddModelError(string.Empty, $"El dominio {nombre} ya está registrado.");
        if (!ServicioFinanzas.Monedas.Contains(input.Moneda)) input.Moneda = "USD";
        if (input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId)) input.ProductoId = null;
        if (input.RegistradorId is not null && !await _db.ServiciosExternos.AnyAsync(s => s.Id == input.RegistradorId)) input.RegistradorId = null;
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Modal = await ModalAsync(id is null ? null : d, input);
            return Page();
        }
        d.Nombre = nombre;
        d.ProductoId = input.ProductoId;
        d.RegistradorId = input.RegistradorId;
        d.Dns = Opciones.Limpio(input.Dns);
        d.FechaVencimiento = input.FechaVencimiento;
        d.AniosRenovacion = input.AniosRenovacion;
        d.RenovacionAutomatica = input.RenovacionAutomatica;
        d.Costo = input.Costo is > 0 ? input.Costo : null;
        d.Moneda = input.Moneda;
        d.SslVence = input.SslVence;
        d.SslEmisor = Opciones.Limpio(input.SslEmisor);
        d.Estado = input.Estado;
        d.Notas = Opciones.Limpio(input.Notas);
        if (id is null) _db.Dominios.Add(d);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess(id is null ? $"Dominio {d.Nombre} registrado." : $"Dominio {d.Nombre} actualizado.");
        if (d.Costo is not null && d.FechaVencimiento <= Hoy.AddDays(30))
            this.ToastInfo("Vence en menos de 30 días: registra su cargo en Finanzas › Movimientos.");
        return RedirectToPage("Index", new { vista = Vista });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var d = await _db.Dominios.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (await _db.Movimientos.AnyAsync(m => m.DominioId == id))
        {
            this.ToastWarning($"{d.Nombre} tiene cargos en Finanzas. Márcalo como Cancelado o Transferido.");
            return RedirectToPage("Index");
        }
        _db.Dominios.Remove(d);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Dominio {d.Nombre} eliminado.");
        return RedirectToPage("Index");
    }

    private static string Normalizar(string? valor)
    {
        var v = (valor ?? string.Empty).Trim().ToLowerInvariant();
        v = Regex.Replace(v, "^https?://", "");
        v = v.Split('/')[0];
        return v.StartsWith("www.") ? v[4..] : v;
    }

    [GeneratedRegex(@"^(?=.{3,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$")]
    private static partial Regex NombreValido();

    private async Task<DominioModalVm> ModalAsync(Dominio? d, DominioInput input) => new()
    {
        Id = d?.Id,
        Input = input,
        Cargos = d is null ? 0 : await _db.Movimientos.CountAsync(m => m.DominioId == d.Id),
        Productos = await Listas.ProductosAsync(_db),
        Servicios = await Listas.ServiciosAsync(_db),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = d?.CreadoUtc,
        CreadoPor = d?.CreadoPor,
        ModificadoUtc = d?.ModificadoUtc,
        ModificadoPor = d?.ModificadoPor
    };

    private async Task CargarAsync()
    {
        var todos = await _db.Dominios.AsNoTracking()
            .Select(d => new Fila(d, d.Producto == null ? null : d.Producto.Nombre, d.Registrador == null ? null : d.Registrador.Nombre))
            .ToListAsync();
        var activos = todos.Where(f => f.Dominio.Estado == EstadoDominio.Activo).ToList();
        Total = todos.Count;
        Activos = activos.Count;
        PorVencer = activos.Count(f => Proximo(f.Dominio.FechaVencimiento) || Proximo(f.Dominio.SslVence));
        CostoAnualUsd = activos.Where(f => f.Dominio.Moneda == "USD").Sum(f => (f.Dominio.Costo ?? 0) / Math.Max(1, f.Dominio.AniosRenovacion));
        CostoAnualMxn = activos.Where(f => f.Dominio.Moneda == "MXN").Sum(f => (f.Dominio.Costo ?? 0) / Math.Max(1, f.Dominio.AniosRenovacion));
        IEnumerable<Fila> q = Vista switch
        {
            "porvencer" => activos.Where(f => Proximo(f.Dominio.FechaVencimiento) || Proximo(f.Dominio.SslVence)),
            "todos" => todos,
            _ => activos
        };
        Dominios = q.OrderBy(f => f.Dominio.FechaVencimiento ?? DateOnly.MaxValue).ThenBy(f => f.Dominio.Nombre).ToList();
    }
}
