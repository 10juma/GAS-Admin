using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Productos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Productos.Cuentas;

public class CuentaModalVm
{
    public CuentaProducto Cuenta { get; init; } = null!;
    public SuscriptorStripe? Suscriptor { get; init; }
    public decimal? MrrStripe { get; init; }
    public string? MonedaStripe { get; init; }
    public List<(int Id, string Nombre)> Clientes { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public bool PuedeVerFinanzas { get; init; }
    public string? Volver { get; init; }
}

[Authorize(Policy = Permisos.Productos.Ver)]
public class IndexModel : PageModel
{
    private const int Limite = 300;

    private readonly GasDbContext _db;
    private readonly ServicioConexiones _conexiones;
    private readonly IAuditoria _auditoria;

    public IndexModel(GasDbContext db, ServicioConexiones conexiones, IAuditoria auditoria)
    {
        _db = db;
        _conexiones = conexiones;
        _auditoria = auditoria;
    }

    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }
    /// <summary>vigentes (por defecto), prueba, riesgo, suspendidas, canceladas o todas.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Productos.Gestionar);

    public List<(int ProductoId, string Nombre)> Productos { get; private set; } = [];
    public List<CuentaProducto> Cuentas { get; private set; } = [];
    public Dictionary<string, int> Conteos { get; private set; } = [];
    public int PruebasPorVencer { get; private set; }
    public bool HayMas { get; private set; }
    public DateTime Hace30 { get; } = DateTime.UtcNow.AddDays(-30);

    public string VolverActual => Request.Path + Request.QueryString;

    public async Task OnGetAsync()
    {
        Productos = (await _db.ConexionesProducto.AsNoTracking().Include(c => c.Producto).OrderBy(c => c.Producto.Nombre)
            .Select(c => new { c.ProductoId, c.Producto.Nombre }).ToListAsync()).Select(x => (x.ProductoId, x.Nombre)).ToList();

        var q = _db.CuentasProducto.AsNoTracking().Include(c => c.Conexion).ThenInclude(c => c.Producto).Include(c => c.Cliente).AsQueryable();
        if (Producto is not null) q = q.Where(c => c.Conexion.ProductoId == Producto);
        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(c => c.Nombre.Contains(b) || (c.Email != null && c.Email.Contains(b)) || c.ExternoId == b || (c.Plan != null && c.Plan.Contains(b)));
        }

        var hace30 = Hace30;
        var en7 = DateTime.UtcNow.AddDays(7);
        var k = await q.GroupBy(_ => 1).Select(g => new
        {
            Vigentes = g.Count(c => c.Estado == "activa" || c.Estado == "prueba"),
            Prueba = g.Count(c => c.Estado == "prueba"),
            Riesgo = g.Count(c => c.Estado == "activa" && (c.UltimoUsoUtc == null || c.UltimoUsoUtc < hace30)),
            Suspendidas = g.Count(c => c.Estado == "suspendida"),
            Canceladas = g.Count(c => c.Estado == "cancelada"),
            Todas = g.Count(),
            PorVencer = g.Count(c => c.Estado == "prueba" && c.PruebaHastaUtc <= en7)
        }).FirstOrDefaultAsync();
        Conteos = new()
        {
            ["vigentes"] = k?.Vigentes ?? 0,
            ["prueba"] = k?.Prueba ?? 0,
            ["riesgo"] = k?.Riesgo ?? 0,
            ["suspendidas"] = k?.Suspendidas ?? 0,
            ["canceladas"] = k?.Canceladas ?? 0,
            ["todas"] = k?.Todas ?? 0
        };
        PruebasPorVencer = k?.PorVencer ?? 0;

        q = Vista switch
        {
            "prueba" => q.Where(c => c.Estado == "prueba"),
            "riesgo" => q.Where(c => c.Estado == "activa" && (c.UltimoUsoUtc == null || c.UltimoUsoUtc < hace30)),
            "suspendidas" => q.Where(c => c.Estado == "suspendida"),
            "canceladas" => q.Where(c => c.Estado == "cancelada"),
            "todas" => q,
            _ => q.Where(c => c.Estado == "activa" || c.Estado == "prueba")
        };
        q = Vista == "prueba" ? q.OrderBy(c => c.PruebaHastaUtc) : q.OrderByDescending(c => c.UltimoUsoUtc).ThenBy(c => c.Nombre);
        var lista = await q.Take(Limite + 1).ToListAsync();
        HayMas = lista.Count > Limite;
        Cuentas = lista.Take(Limite).ToList();
    }

    public async Task<IActionResult> OnGetCuentaAsync(int id, string? volver)
    {
        var c = await _db.CuentasProducto.AsNoTracking().Include(x => x.Conexion).ThenInclude(x => x.Producto).Include(x => x.Cliente)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        SuscriptorStripe? sus = null;
        decimal? mrr = null;
        string? moneda = null;
        if (c.StripeCustomerId is not null)
        {
            sus = await _db.SuscriptoresStripe.AsNoTracking().FirstOrDefaultAsync(s => s.StripeId == c.StripeCustomerId && s.Cuenta.ProductoId == c.Conexion.ProductoId);
            if (sus is not null)
            {
                var subs = await _db.SuscripcionesStripe.AsNoTracking().Where(s => s.SuscriptorId == sus.Id && (s.Estado == "active" || s.Estado == "past_due"))
                    .Select(s => new { s.MontoMensual, s.Moneda }).ToListAsync();
                mrr = subs.Sum(s => s.MontoMensual);
                moneda = subs.Select(s => s.Moneda).FirstOrDefault() ?? "MXN";
            }
        }
        return Partial("_CuentaModal", new CuentaModalVm
        {
            Cuenta = c, Suscriptor = sus, MrrStripe = mrr, MonedaStripe = moneda,
            Clientes = (await _db.Clientes.AsNoTracking().OrderBy(x => x.NombreComercial ?? x.Nombre)
                .Select(x => new { x.Id, Nombre = x.NombreComercial ?? x.Nombre }).ToListAsync()).Select(x => (x.Id, x.Nombre)).ToList(),
            PuedeGestionar = PuedeGestionar,
            PuedeVerFinanzas = User.TienePermiso(Permisos.Finanzas.Ver),
            Volver = volver
        });
    }

    public async Task<IActionResult> OnPostVincularAsync(int id, int? clienteId, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.CuentasProducto.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        c.ClienteId = clienteId is not null && await _db.Clientes.AnyAsync(x => x.Id == clienteId) ? clienteId : null;
        await _db.SaveChangesAsync();
        this.ToastSuccess(c.ClienteId is null ? $"{c.Nombre} ya no está ligada a un cliente." : $"{c.Nombre} quedó ligada al cliente.");
        return Volver(volver);
    }

    public async Task<IActionResult> OnPostAccionAsync(int id, string accion, int? dias, string? motivo, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var def = ServicioConexiones.Acciones.FirstOrDefault(a => a.Clave == accion);
        if (def is null) return BadRequest();
        motivo = motivo?.Trim();
        if (string.IsNullOrEmpty(motivo) || motivo.Length < 5)
        {
            this.ToastWarning("Escribe el motivo de la acción (queda en la bitácora de GAS-Admin y del producto).");
            return Volver(volver);
        }
        if (motivo.Length > 500) motivo = motivo[..500];
        if (def.PideDias && dias is null or < 1 or > 90)
        {
            this.ToastWarning("Indica de 1 a 90 días para extender la prueba.");
            return Volver(volver);
        }

        var cuenta = await _db.CuentasProducto.AsNoTracking().Include(x => x.Conexion).ThenInclude(x => x.Producto).FirstOrDefaultAsync(x => x.Id == id);
        if (cuenta is null) return NotFound();
        var (ok, mensaje) = await _conexiones.EjecutarAccionAsync(id, accion, def.PideDias ? dias : null, motivo, User.Identity?.Name ?? "desconocido");

        await _auditoria.RegistrarAsync(AccionesAuditoria.AccionEnProducto,
            detalle: $"{def.Texto}{(def.PideDias ? $" ({dias} días)" : "")} · {cuenta.Conexion.Producto.Nombre} · {cuenta.Nombre} · Motivo: {motivo} · Resultado: {(ok ? "aplicada" : "rechazada")} — {mensaje}",
            entidad: "Cuenta de producto", entidadId: cuenta.ExternoId);

        if (ok) this.ToastSuccess($"{cuenta.Nombre}: {mensaje}");
        else this.ToastError($"{cuenta.Nombre}: {mensaje}");
        return Volver(volver);
    }

    private IActionResult Volver(string? volver) =>
        !string.IsNullOrEmpty(volver) && Url.IsLocalUrl(volver) ? LocalRedirect(volver) : RedirectToPage("Index");
}
