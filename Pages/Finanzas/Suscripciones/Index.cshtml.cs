using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Finanzas;
using GAS.Admin.Services.Proyectos;
using GAS.Admin.Services.Stripe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Finanzas.Suscripciones;

public sealed record OpcionCliente(int Id, string Nombre);

public class SuscriptorModalVm
{
    public SuscriptorStripe Suscriptor { get; init; } = null!;
    public string Producto { get; init; } = string.Empty;
    public List<SuscripcionStripe> Suscripciones { get; init; } = [];
    public List<FacturaStripe> Facturas { get; init; } = [];
    public List<OpcionCliente> Clientes { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public string? Volver { get; init; }
}

[Authorize(Policy = Permisos.Finanzas.Ver)]
public class IndexModel : PageModel
{
    private const int Limite = 300;

    private readonly GasDbContext _db;
    private readonly ServicioStripe _stripe;
    private readonly ServicioFinanzas _finanzas;

    public IndexModel(GasDbContext db, ServicioStripe stripe, ServicioFinanzas finanzas)
    {
        _db = db;
        _stripe = stripe;
        _finanzas = finanzas;
    }

    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }
    /// <summary>suscripciones (por defecto), fallidas o suscriptores.</summary>
    [BindProperty(SupportsGet = true)] public string? Seccion { get; set; }
    /// <summary>Filtro de suscripciones: vigentes (por defecto), prueba, atrasadas, canceladas o todas.</summary>
    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Finanzas.Gestionar);

    public sealed record FilaProducto(int ProductoId, string Producto, int Activas, int EnPrueba, int Atrasadas, decimal Mrr, int Fallidas, CuentaStripe Cuenta)
    {
        public decimal? Arpu => Activas + Atrasadas > 0 ? Math.Round(Mrr / (Activas + Atrasadas), 2) : null;
    }
    public sealed record PuntoMrr(DateOnly Fecha, decimal Mrr);

    public List<CuentaStripe> Cuentas { get; private set; } = [];
    public List<FilaProducto> PorProducto { get; private set; } = [];
    public List<PuntoMrr> Tendencia { get; private set; } = [];
    public decimal Mrr { get; private set; }
    public int Activas { get; private set; }
    public int EnPrueba { get; private set; }
    public int Atrasadas { get; private set; }
    public int Nuevas { get; private set; }
    public int Canceladas { get; private set; }
    public decimal? Churn { get; private set; }
    public decimal? Arpu { get; private set; }
    public int FallidasTotal { get; private set; }
    public decimal MontoFallido { get; private set; }
    public bool FaltaTipoCambio { get; private set; }
    public Dictionary<string, int> Conteos { get; private set; } = [];

    public List<SuscripcionStripe> Suscripciones { get; private set; } = [];
    public List<FacturaStripe> Fallidas { get; private set; } = [];
    public List<(SuscriptorStripe S, int Vigentes, decimal Mrr, string Moneda, string? Cliente)> Suscriptores { get; private set; } = [];
    public bool HayMas { get; private set; }
    public SuscriptorModalVm? Modal { get; private set; }

    public string VolverActual => Request.Path + Request.QueryString;

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostSincronizarAsync(string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var resultados = await _stripe.SincronizarTodasAsync();
        if (resultados.Count == 0) this.ToastInfo("No hay cuentas de Stripe activas. Conéctalas en Finanzas › Configuración › Stripe.");
        foreach (var r in resultados)
        {
            if (r.Exito) this.ToastSuccess($"{r.Cuenta}: {r.Mensaje}");
            else this.ToastError($"{r.Cuenta}: {r.Mensaje}");
        }
        return !string.IsNullOrEmpty(volver) && Url.IsLocalUrl(volver) ? LocalRedirect(volver) : RedirectToPage("Index");
    }

    public async Task<IActionResult> OnGetSuscriptorAsync(int id, string? volver)
    {
        var s = await _db.SuscriptoresStripe.AsNoTracking().Include(x => x.Cuenta).ThenInclude(c => c.Producto).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        return Partial("_SuscriptorModal", await SuscriptorVmAsync(s, volver));
    }

    public async Task<IActionResult> OnPostVincularAsync(int id, int? clienteId, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var s = await _db.SuscriptoresStripe.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (clienteId is not null && !await _db.Clientes.AnyAsync(c => c.Id == clienteId)) clienteId = null;
        s.ClienteId = clienteId;
        // Los cobros ya registrados en Finanzas también quedan ligados al cliente.
        var movimientos = await _db.FacturasStripe.Where(f => f.SuscriptorId == id && f.MovimientoIngresoId != null)
            .Select(f => f.MovimientoIngresoId!.Value).ToListAsync();
        await _db.Movimientos.Where(m => movimientos.Contains(m.Id)).ForEachAsync(m => m.ClienteId = clienteId);
        await _db.SaveChangesAsync();
        this.ToastSuccess(clienteId is null ? $"{s.Visible} ya no está ligado a un cliente." : $"{s.Visible} quedó ligado al cliente.");
        return !string.IsNullOrEmpty(volver) && Url.IsLocalUrl(volver) ? LocalRedirect(volver) : RedirectToPage("Index", new { seccion = "suscriptores" });
    }

    private async Task<SuscriptorModalVm> SuscriptorVmAsync(SuscriptorStripe s, string? volver) => new()
    {
        Suscriptor = s,
        Producto = s.Cuenta.Producto.Nombre,
        Suscripciones = await _db.SuscripcionesStripe.AsNoTracking().Where(x => x.SuscriptorId == s.Id).OrderByDescending(x => x.InicioUtc).ToListAsync(),
        Facturas = await _db.FacturasStripe.AsNoTracking().Where(f => f.SuscriptorId == s.Id).OrderByDescending(f => f.FechaUtc).Take(12).ToListAsync(),
        Clientes = await _db.Clientes.AsNoTracking().OrderBy(c => c.NombreComercial ?? c.Nombre).Select(c => new OpcionCliente(c.Id, c.NombreComercial ?? c.Nombre)).ToListAsync(),
        PuedeGestionar = PuedeGestionar,
        Volver = volver
    };

    private async Task<decimal> MxnAsync(decimal monto, string moneda)
    {
        var r = await _finanzas.AMxnAsync(monto, moneda);
        if (r is null) FaltaTipoCambio = true;
        return r ?? 0;
    }

    private async Task CargarAsync()
    {
        var hoy = ServicioFinanzas.Hoy;
        var inicioMes = Fechas.ALocalUtc(ServicioTrabajos.InicioMes(hoy).ToDateTime(TimeOnly.MinValue));

        Cuentas = await _db.CuentasStripe.AsNoTracking().Include(c => c.Producto).OrderBy(c => c.Producto.Nombre).ToListAsync();
        var ids = Cuentas.Where(c => Producto is null || c.ProductoId == Producto).Select(c => c.Id).ToList();

        var todas = await _db.SuscripcionesStripe.AsNoTracking().Where(s => ids.Contains(s.CuentaStripeId))
            .Select(s => new { s.CuentaStripeId, s.Estado, s.MontoMensual, s.Moneda, s.InicioUtc, s.CanceladaUtc }).ToListAsync();
        var fallidas = await _db.FacturasStripe.AsNoTracking().Where(f => ids.Contains(f.CuentaStripeId)
                && ((f.Estado == "open" && f.Intentos > 0) || f.Estado == "uncollectible"))
            .Select(f => new { f.CuentaStripeId, f.Total, f.Pagado, f.Moneda }).ToListAsync();

        foreach (var c in Cuentas.Where(c => ids.Contains(c.Id)))
        {
            var sus = todas.Where(s => s.CuentaStripeId == c.Id).ToList();
            decimal mrr = 0;
            foreach (var s in sus.Where(s => s.Estado is "active" or "past_due")) mrr += await MxnAsync(s.MontoMensual, s.Moneda);
            PorProducto.Add(new FilaProducto(c.ProductoId, c.Producto.Nombre,
                sus.Count(s => s.Estado == "active"), sus.Count(s => s.Estado == "trialing"),
                sus.Count(s => s.Estado is "past_due" or "unpaid"), Math.Round(mrr, 2),
                fallidas.Count(f => f.CuentaStripeId == c.Id), c));
        }
        Mrr = PorProducto.Sum(p => p.Mrr);
        Activas = PorProducto.Sum(p => p.Activas);
        EnPrueba = PorProducto.Sum(p => p.EnPrueba);
        Atrasadas = PorProducto.Sum(p => p.Atrasadas);
        Arpu = Activas + Atrasadas > 0 ? Math.Round(Mrr / (Activas + Atrasadas), 2) : null;
        Nuevas = todas.Count(s => s.InicioUtc >= inicioMes && s.Estado != "incomplete" && s.Estado != "incomplete_expired");
        Canceladas = todas.Count(s => s.CanceladaUtc >= inicioMes);
        var baseInicio = Activas + Atrasadas + Canceladas - Nuevas;
        Churn = baseInicio > 0 ? Math.Round((decimal)Canceladas / baseInicio, 4) : null;
        FallidasTotal = fallidas.Count;
        foreach (var f in fallidas) MontoFallido += await MxnAsync(f.Total - f.Pagado, f.Moneda);

        var desde = hoy.AddDays(-90);
        Tendencia = (await _db.MetricasStripe.AsNoTracking().Where(m => ids.Contains(m.CuentaStripeId) && m.Fecha >= desde)
                .GroupBy(m => m.Fecha).Select(g => new { g.Key, Mrr = g.Sum(x => x.Mrr) }).ToListAsync())
            .OrderBy(x => x.Key).Select(x => new PuntoMrr(x.Key, x.Mrr)).ToList();

        Conteos = new()
        {
            ["vigentes"] = todas.Count(s => s.Estado is "active" or "past_due" or "trialing" or "unpaid" or "paused"),
            ["prueba"] = EnPrueba,
            ["atrasadas"] = Atrasadas,
            ["canceladas"] = todas.Count(s => s.Estado is "canceled" or "incomplete_expired"),
            ["todas"] = todas.Count
        };

        var b = Buscar?.Trim();
        switch (Seccion)
        {
            case "fallidas":
                Fallidas = await _db.FacturasStripe.AsNoTracking().Include(f => f.Suscriptor).Include(f => f.Suscripcion).Include(f => f.Cuenta).ThenInclude(c => c.Producto)
                    .Where(f => ids.Contains(f.CuentaStripeId) && ((f.Estado == "open" && f.Intentos > 0) || f.Estado == "uncollectible"))
                    .OrderByDescending(f => f.FechaUtc).Take(Limite).ToListAsync();
                break;

            case "suscriptores":
                var q = _db.SuscriptoresStripe.AsNoTracking().Include(s => s.Cuenta).ThenInclude(c => c.Producto).Where(s => ids.Contains(s.CuentaStripeId));
                if (!string.IsNullOrEmpty(b)) q = q.Where(s => (s.Nombre != null && s.Nombre.Contains(b)) || (s.Email != null && s.Email.Contains(b)) || s.StripeId == b);
                var lista = await q.Select(s => new
                    {
                        S = s,
                        Vigentes = s.Suscripciones.Count(x => x.Estado == "active" || x.Estado == "past_due" || x.Estado == "trialing" || x.Estado == "unpaid"),
                        Mrr = s.Suscripciones.Where(x => x.Estado == "active" || x.Estado == "past_due").Sum(x => (decimal?)x.MontoMensual) ?? 0,
                        Moneda = s.Suscripciones.Select(x => x.Moneda).FirstOrDefault() ?? "MXN",
                        Cliente = s.Cliente == null ? null : (s.Cliente.NombreComercial ?? s.Cliente.Nombre)
                    })
                    .OrderByDescending(x => x.Mrr).ThenBy(x => x.S.Nombre).Take(Limite + 1).ToListAsync();
                HayMas = lista.Count > Limite;
                Suscriptores = lista.Take(Limite).Select(x => (x.S, x.Vigentes, x.Mrr, x.Moneda, x.Cliente)).ToList();
                break;

            default:
                var qs = _db.SuscripcionesStripe.AsNoTracking().Include(s => s.Suscriptor).Include(s => s.Cuenta).ThenInclude(c => c.Producto)
                    .Where(s => ids.Contains(s.CuentaStripeId));
                qs = Estado switch
                {
                    "prueba" => qs.Where(s => s.Estado == "trialing"),
                    "atrasadas" => qs.Where(s => s.Estado == "past_due" || s.Estado == "unpaid"),
                    "canceladas" => qs.Where(s => s.Estado == "canceled" || s.Estado == "incomplete_expired"),
                    "todas" => qs,
                    _ => qs.Where(s => s.Estado == "active" || s.Estado == "past_due" || s.Estado == "trialing" || s.Estado == "unpaid" || s.Estado == "paused")
                };
                if (!string.IsNullOrEmpty(b))
                    qs = qs.Where(s => (s.Plan != null && s.Plan.Contains(b)) || (s.Suscriptor != null && ((s.Suscriptor.Nombre != null && s.Suscriptor.Nombre.Contains(b)) || (s.Suscriptor.Email != null && s.Suscriptor.Email.Contains(b)))));
                var subs = await qs.OrderByDescending(s => s.MontoMensual).ThenByDescending(s => s.InicioUtc).Take(Limite + 1).ToListAsync();
                HayMas = subs.Count > Limite;
                Suscripciones = subs.Take(Limite).ToList();
                break;
        }
    }
}
