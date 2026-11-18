using System.Globalization;
using System.Text.Json;
using GAS.Admin.Data.Context;
using GAS.Admin.Data.Seed;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Finanzas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Stripe;

public sealed record ResultadoSincronizacion(string Cuenta, bool Exito, string Mensaje);

/// <summary>
/// Sincroniza cada cuenta de Stripe (una por producto): suscripciones, suscriptores y facturas;
/// registra en Finanzas los cobros pagados con su comisión y guarda la foto diaria del MRR.
/// </summary>
public class ServicioStripe
{
    /// <summary>Evita que la sincronización automática y la manual corran al mismo tiempo.</summary>
    private static readonly SemaphoreSlim Candado = new(1, 1);

    private readonly GasDbContext _db;
    private readonly ClienteStripe _stripe;
    private readonly ServicioFinanzas _finanzas;
    private readonly IOptionsMonitor<OpcionesStripe> _monitorOpciones;
    private OpcionesStripe _opciones => _monitorOpciones.CurrentValue; // siempre la configuración vigente
    private readonly ILogger<ServicioStripe> _logger;

    public ServicioStripe(GasDbContext db, ClienteStripe stripe, ServicioFinanzas finanzas, IOptionsMonitor<OpcionesStripe> opciones, ILogger<ServicioStripe> logger)
    {
        _db = db;
        _stripe = stripe;
        _finanzas = finanzas;
        _monitorOpciones = opciones;
        _logger = logger;
    }

    /// <summary>Si la clave está configurada (sin revelarla) y de qué tipo es.</summary>
    public (bool Configurada, bool Restringida, bool Prueba) EstadoClave(string clave) =>
        _opciones.Claves.TryGetValue(clave, out var k) && !string.IsNullOrWhiteSpace(k)
            ? (true, k.StartsWith("rk_"), k.Contains("_test_"))
            : (false, false, false);

    public async Task<List<ResultadoSincronizacion>> SincronizarTodasAsync(CancellationToken ct = default)
    {
        var ids = await _db.CuentasStripe.Where(c => c.Activa).Select(c => c.Id).ToListAsync(ct);
        var resultados = new List<ResultadoSincronizacion>();
        foreach (var id in ids) resultados.Add(await SincronizarAsync(id, ct));
        return resultados;
    }

    public async Task<ResultadoSincronizacion> SincronizarAsync(int cuentaId, CancellationToken ct = default)
    {
        if (!await Candado.WaitAsync(TimeSpan.FromMinutes(5), ct))
            return new("Stripe", false, "Ya hay una sincronización en curso.");
        try
        {
            var c = await _db.CuentasStripe.Include(x => x.Producto).FirstAsync(x => x.Id == cuentaId, ct);
            if (!_opciones.Claves.TryGetValue(c.Clave, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            {
                c.UltimoError = "Falta la API key: un Admin debe capturarla en Finanzas › Configuración › Stripe.";
                await _db.SaveChangesAsync(ct);
                return new(c.Producto.Nombre, false, c.UltimoError);
            }
            try
            {
                var mensaje = await SincronizarCuentaAsync(c, apiKey, ct);
                c.UltimaSincronizacionUtc = DateTime.UtcNow;
                c.UltimoResultado = mensaje;
                c.UltimoError = null;
                await _db.SaveChangesAsync(ct);
                return new(c.Producto.Nombre, true, mensaje);
            }
            catch (Exception ex) when (ex is ErrorStripe or HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                _logger.LogWarning(ex, "Error al sincronizar Stripe de {Producto}", c.Producto.Nombre);
                _db.ChangeTracker.Clear();
                var cuenta = await _db.CuentasStripe.FirstAsync(x => x.Id == cuentaId, CancellationToken.None);
                cuenta.UltimoError = ex is ErrorStripe ? ex.Message : $"No se pudo sincronizar: {ex.Message}";
                if (cuenta.UltimoError.Length > 1000) cuenta.UltimoError = cuenta.UltimoError[..1000];
                await _db.SaveChangesAsync(CancellationToken.None);
                return new(c.Producto.Nombre, false, cuenta.UltimoError);
            }
        }
        finally
        {
            Candado.Release();
        }
    }

    private async Task<string> SincronizarCuentaAsync(CuentaStripe c, string apiKey, CancellationToken ct)
    {
        // ---------- Cuenta ----------
        var cuenta = await _stripe.ObtenerAsync(apiKey, "/v1/account", ct);
        c.CuentaId = cuenta.Texto("id");
        c.NombreStripe = cuenta.Prop("settings")?.Prop("dashboard")?.Texto("display_name")
                         ?? cuenta.Prop("business_profile")?.Texto("name");
        c.ModoPrueba = apiKey.Contains("_test_");

        var suscriptores = await _db.SuscriptoresStripe.Where(s => s.CuentaStripeId == c.Id).ToDictionaryAsync(s => s.StripeId, ct);
        var suscripciones = await _db.SuscripcionesStripe.Where(s => s.CuentaStripeId == c.Id).ToDictionaryAsync(s => s.StripeId, ct);

        SuscriptorStripe Suscriptor(string id, string? nombre, string? email, DateTime? creado)
        {
            if (!suscriptores.TryGetValue(id, out var s))
            {
                s = new SuscriptorStripe { Cuenta = c, StripeId = id };
                suscriptores[id] = s;
                _db.SuscriptoresStripe.Add(s);
            }
            s.Nombre = Recortar(nombre, 200) ?? s.Nombre;
            s.Email = Recortar(email, 256) ?? s.Email;
            s.CreadoStripeUtc = creado ?? s.CreadoStripeUtc;
            s.ActualizadoUtc = DateTime.UtcNow;
            return s;
        }

        // ---------- Suscripciones (con cliente y producto expandidos) ----------
        var nSus = 0;
        await foreach (var j in _stripe.ListarAsync(apiKey,
                           "/v1/subscriptions?status=all&expand[]=data.customer&expand[]=data.items.data.price.product", ct))
        {
            nSus++;
            var id = j.Texto("id")!;
            if (!suscripciones.TryGetValue(id, out var s))
            {
                s = new SuscripcionStripe { Cuenta = c, StripeId = id };
                suscripciones[id] = s;
                _db.SuscripcionesStripe.Add(s);
            }
            var cliente = j.Prop("customer");
            if (cliente is { ValueKind: JsonValueKind.Object } co)
                s.Suscriptor = Suscriptor(co.Texto("id")!, co.Texto("name"), co.Texto("email"), co.Fecha("created"));
            else if (j.Texto("customer") is { } cid)
                s.Suscriptor = Suscriptor(cid, null, null, null);

            var moneda = (j.Texto("currency") ?? "mxn").ToUpperInvariant();
            decimal mensual = 0;
            string? plan = null, intervalo = null;
            int cada = 1, cantidad = 0;
            if (j.Prop("items")?.Prop("data") is { ValueKind: JsonValueKind.Array } items)
            {
                foreach (var it in items.EnumerateArray())
                {
                    var q = (int)Math.Max(1, it.Entero("quantity"));
                    cantidad += q;
                    var precio = it.Prop("price");
                    if (precio is not { } p) continue;
                    var producto = p.Prop("product");
                    var nombre = producto is { ValueKind: JsonValueKind.Object } po ? po.Texto("name") : null;
                    plan ??= string.Join(" · ", new[] { nombre, p.Texto("nickname") }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    // Un concepto sin periodo (cobro único) no suma al MRR ni define el intervalo.
                    if (p.Prop("recurring") is not { } recurrente) continue;
                    var intervaloItem = recurrente.Texto("interval") ?? "month";
                    var cadaItem = (int)Math.Max(1, recurrente.Entero("interval_count"));
                    intervalo ??= intervaloItem;
                    cada = intervalo == intervaloItem ? cadaItem : cada;
                    var unitario = ClienteStripe.Importe(p.Entero("unit_amount"), moneda) * q;
                    mensual += intervaloItem switch
                    {
                        "year" => unitario / (12m * cadaItem),
                        "week" => unitario * 52m / (12m * cadaItem),
                        "day" => unitario * 365m / (12m * cadaItem),
                        _ => unitario / cadaItem
                    };
                }
            }
            // Cupón vigente: porcentaje o monto fijo por periodo (se lleva a su equivalente mensual).
            if (j.Prop("discount")?.Prop("coupon") is { } cupon && mensual > 0)
            {
                if (cupon.Prop("percent_off") is { ValueKind: JsonValueKind.Number } pct)
                    mensual *= 1 - Math.Clamp(pct.GetDecimal(), 0, 100) / 100m;
                else if (cupon.Entero("amount_off") > 0 && (cupon.Texto("currency") ?? moneda.ToLowerInvariant()).Equals(moneda, StringComparison.OrdinalIgnoreCase))
                {
                    var meses = intervalo switch { "year" => 12m * cada, "week" => 12m * cada / 52m, "day" => 12m * cada / 365m, _ => cada };
                    mensual = Math.Max(0, mensual - ClienteStripe.Importe(cupon.Entero("amount_off"), moneda) / meses);
                }
            }
            s.Estado = j.Texto("status") ?? "unknown";
            s.Plan = Recortar(string.IsNullOrWhiteSpace(plan) ? null : plan, 200);
            s.Moneda = moneda;
            s.MontoMensual = Math.Round(mensual, 2);
            s.Intervalo = intervalo;
            s.IntervaloCantidad = cada;
            s.Cantidad = Math.Max(1, cantidad);
            s.InicioUtc = j.Fecha("start_date");
            s.PeriodoFinUtc = j.Fecha("current_period_end");
            s.PruebaHastaUtc = j.Fecha("trial_end");
            s.CancelarAlFinal = j.Booleano("cancel_at_period_end");
            s.CanceladaUtc = j.Fecha("canceled_at");
            s.ActualizadoUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);

        // ---------- Facturas (con el cargo y su comisión) ----------
        var desde = c.UltimaSincronizacionUtc is null ? DateTime.UtcNow.AddMonths(-13) : DateTime.UtcNow.AddDays(-60);
        var facturas = await _db.FacturasStripe.Where(f => f.CuentaStripeId == c.Id && f.FechaUtc >= desde.AddDays(-1))
            .ToDictionaryAsync(f => f.StripeId, ct);
        var nFac = 0;
        await foreach (var j in _stripe.ListarAsync(apiKey,
                           $"/v1/invoices?created[gte]={new DateTimeOffset(desde).ToUnixTimeSeconds()}&expand[]=data.charge.balance_transaction", ct))
        {
            nFac++;
            var id = j.Texto("id")!;
            if (!facturas.TryGetValue(id, out var f))
            {
                f = await _db.FacturasStripe.FirstOrDefaultAsync(x => x.CuentaStripeId == c.Id && x.StripeId == id, ct)
                    ?? _db.FacturasStripe.Add(new FacturaStripe { Cuenta = c, StripeId = id }).Entity;
                facturas[id] = f;
            }
            var moneda = (j.Texto("currency") ?? "mxn").ToUpperInvariant();
            f.Numero = Recortar(j.Texto("number"), 100);
            f.Estado = j.Texto("status") ?? "draft";
            f.Moneda = moneda;
            f.Total = ClienteStripe.Importe(j.Entero("total"), moneda);
            f.Impuestos = ClienteStripe.Importe(j.Entero("tax"), moneda);
            f.Pagado = ClienteStripe.Importe(j.Entero("amount_paid"), moneda);
            f.FechaUtc = j.Fecha("created") ?? DateTime.UtcNow;
            f.PagadaUtc = j.Prop("status_transitions")?.Fecha("paid_at");
            f.Intentos = (int)j.Entero("attempt_count");
            f.ProximoIntentoUtc = j.Fecha("next_payment_attempt");
            f.Url = Recortar(j.Texto("hosted_invoice_url"), 500);
            if (j.Id("customer") is { } cid) f.Suscriptor = Suscriptor(cid, j.Texto("customer_name"), j.Texto("customer_email"), null);
            if (j.Id("subscription") is { } sid && suscripciones.TryGetValue(sid, out var sus)) f.Suscripcion = sus;
            if (j.Prop("charge")?.Prop("balance_transaction") is { ValueKind: JsonValueKind.Object } bt)
            {
                var monedaFee = (bt.Texto("currency") ?? moneda).ToUpperInvariant();
                f.Comision = ClienteStripe.Importe(bt.Entero("fee"), monedaFee);
                f.MonedaComision = monedaFee;
            }
            f.ActualizadoUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);

        // Facturas viejas que seguían abiertas: se consultan una por una por si se pagaron tarde.
        var viejasAbiertas = await _db.FacturasStripe.Where(f => f.CuentaStripeId == c.Id && (f.Estado == "open" || f.Estado == "draft") && f.FechaUtc < desde)
            .OrderByDescending(f => f.FechaUtc).Take(50).ToListAsync(ct);
        foreach (var f in viejasAbiertas)
        {
            var j = await _stripe.ObtenerAsync(apiKey, $"/v1/invoices/{Uri.EscapeDataString(f.StripeId)}?expand[]=charge.balance_transaction", ct);
            var moneda = (j.Texto("currency") ?? f.Moneda).ToUpperInvariant();
            f.Estado = j.Texto("status") ?? f.Estado;
            f.Pagado = ClienteStripe.Importe(j.Entero("amount_paid"), moneda);
            f.PagadaUtc = j.Prop("status_transitions")?.Fecha("paid_at");
            f.Intentos = (int)j.Entero("attempt_count");
            f.ProximoIntentoUtc = j.Fecha("next_payment_attempt");
            if (j.Prop("charge")?.Prop("balance_transaction") is { ValueKind: JsonValueKind.Object } bt)
            {
                f.MonedaComision = (bt.Texto("currency") ?? moneda).ToUpperInvariant();
                f.Comision = ClienteStripe.Importe(bt.Entero("fee"), f.MonedaComision);
            }
            f.ActualizadoUtc = DateTime.UtcNow;
        }
        if (viejasAbiertas.Count > 0) await _db.SaveChangesAsync(ct);

        // ---------- Finanzas: ingreso + comisión de cada cobro nuevo ----------
        var nuevos = await RegistrarEnFinanzasAsync(c, ct);

        // ---------- Foto del día ----------
        await GuardarMetricaAsync(c, suscripciones.Values, ct);

        var modo = c.ModoPrueba ? " (modo de prueba)" : "";
        return $"{nSus} suscripciones y {nFac} facturas revisadas; {nuevos} cobro(s) nuevo(s) en Finanzas{modo}.";
    }

    private async Task<int> RegistrarEnFinanzasAsync(CuentaStripe c, CancellationToken ct)
    {
        var pendientes = await _db.FacturasStripe.Include(f => f.Suscriptor).Include(f => f.Suscripcion)
            .Where(f => f.CuentaStripeId == c.Id && f.Estado == "paid" && f.Pagado > 0 && f.MovimientoIngresoId == null)
            .OrderBy(f => f.FechaUtc).ToListAsync(ct);
        if (pendientes.Count == 0) return 0;

        var catIngreso = await _db.CategoriasFinancieras.Where(x => x.Nombre == FinanzasSeeder.Categorias.Suscripciones && x.Tipo == TipoMovimiento.Ingreso)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        var catComision = await _db.CategoriasFinancieras.Where(x => x.Nombre == FinanzasSeeder.Categorias.Comisiones && x.Tipo == TipoMovimiento.Egreso)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        var servicioStripe = await _db.ServiciosExternos.Where(s => s.Nombre == "Stripe").Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);

        var pares = new List<(FacturaStripe F, Movimiento Ingreso, Movimiento? Comision)>();
        foreach (var f in pendientes)
        {
            var fecha = DateOnly.FromDateTime(Fechas.ALocal(f.PagadaUtc ?? f.FechaUtc));
            var tc = await _finanzas.TipoCambioAsync(f.Moneda, fecha) ?? 0;
            var referencia = f.Numero ?? f.StripeId;
            var ingreso = new Movimiento
            {
                Tipo = TipoMovimiento.Ingreso,
                Estado = EstadoMovimiento.Confirmado,
                Concepto = Recortar($"Stripe · {f.Suscripcion?.Plan ?? "Suscripción"} · {f.Suscriptor?.Visible ?? referencia}", 200)!,
                CategoriaId = catIngreso,
                Linea = LineaNegocio.ProductosSaaS,
                Origen = OrigenMovimiento.Stripe,
                FechaProgramada = fecha,
                FechaPago = fecha,
                Moneda = f.Moneda,
                TipoCambio = f.Moneda == "MXN" ? 1 : tc,
                ProductoId = c.ProductoId,
                ClienteId = f.Suscriptor?.ClienteId,
                Referencia = Recortar(referencia, 100)
            };
            // IVA: si Stripe lo desglosó, se usa; si los precios ya lo incluyen, se separa al 16 %.
            decimal subtotal = f.Pagado, iva = 0;
            if (f.Impuestos > 0) { subtotal = f.Pagado - f.Impuestos; iva = f.Impuestos; }
            // Solo en pesos: una factura en otra moneda se trata como exportación (tasa 0).
            else if (c.PreciosIncluyenIva && f.Moneda == "MXN") { subtotal = Math.Round(f.Pagado / 1.16m, 2); iva = f.Pagado - subtotal; }
            ingreso.Subtotal = subtotal;
            ingreso.TasaIva = subtotal > 0 ? Math.Round(iva / subtotal, 4) : 0;
            ingreso.Iva = iva;
            ingreso.Total = f.Pagado;
            ingreso.SubtotalMxn = Math.Round(subtotal * ingreso.TipoCambio, 2);
            ingreso.TotalMxn = Math.Round(f.Pagado * ingreso.TipoCambio, 2);
            _db.Movimientos.Add(ingreso);

            Movimiento? comision = null;
            if (f.Comision is > 0)
            {
                var monedaFee = f.MonedaComision ?? f.Moneda;
                comision = new Movimiento
                {
                    Tipo = TipoMovimiento.Egreso,
                    Estado = EstadoMovimiento.Confirmado,
                    Concepto = Recortar($"Comisión Stripe · {referencia}", 200)!,
                    CategoriaId = catComision,
                    Linea = LineaNegocio.ProductosSaaS,
                    Origen = OrigenMovimiento.Stripe,
                    FechaProgramada = fecha,
                    FechaPago = fecha,
                    Moneda = monedaFee,
                    Subtotal = f.Comision.Value,
                    TasaIva = 0,
                    TipoCambio = monedaFee == "MXN" ? 1 : await _finanzas.TipoCambioAsync(monedaFee, fecha) ?? 0,
                    ProductoId = c.ProductoId,
                    ServicioExternoId = servicioStripe,
                    Proveedor = "Stripe",
                    Referencia = Recortar(referencia, 100)
                };
                comision.Recalcular();
                _db.Movimientos.Add(comision);
            }
            pares.Add((f, ingreso, comision));
        }
        // En una transacción: si el enlace factura → movimiento falla, no quedan movimientos huérfanos que se dupliquen.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        foreach (var (f, ingreso, comision) in pares)
        {
            f.MovimientoIngresoId = ingreso.Id;
            f.MovimientoComisionId = comision?.Id;
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return pares.Count;
    }

    private async Task GuardarMetricaAsync(CuentaStripe c, IEnumerable<SuscripcionStripe> suscripciones, CancellationToken ct)
    {
        var hoy = ServicioFinanzas.Hoy;
        var lista = suscripciones.ToList();
        decimal mrr = 0;
        foreach (var s in lista.Where(s => s.CuentaEnMrr))
            mrr += await _finanzas.AMxnAsync(s.MontoMensual, s.Moneda, hoy) ?? 0;

        var m = await _db.MetricasStripe.FirstOrDefaultAsync(x => x.CuentaStripeId == c.Id && x.Fecha == hoy, ct);
        if (m is null) _db.MetricasStripe.Add(m = new MetricaStripe { CuentaStripeId = c.Id, Fecha = hoy });
        m.Moneda = "MXN";
        m.Mrr = Math.Round(mrr, 2);
        m.Activas = lista.Count(s => s.Estado == "active");
        m.EnPrueba = lista.Count(s => s.Estado == "trialing");
        m.Atrasadas = lista.Count(s => s.Estado is "past_due" or "unpaid");
        await _db.SaveChangesAsync(ct);
    }

    private static string? Recortar(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Length > max ? s[..max] : s;

    public static string TextoEstado(string estado) => estado switch
    {
        "active" => "Activa",
        "trialing" => "En prueba",
        "past_due" => "Pago atrasado",
        "unpaid" => "Sin pagar",
        "canceled" => "Cancelada",
        "incomplete" => "Incompleta",
        "incomplete_expired" => "Expirada",
        "paused" => "Pausada",
        "paid" => "Pagada",
        "open" => "Abierta",
        "draft" => "Borrador",
        "void" => "Anulada",
        "uncollectible" => "Incobrable",
        _ => estado
    };

    public static string TonoEstado(string estado) => estado switch
    {
        "active" or "paid" => "gas-badge--success",
        "trialing" or "open" => "gas-badge--info",
        "past_due" or "incomplete" or "paused" => "gas-badge--warning",
        "unpaid" or "uncollectible" => "gas-badge--danger",
        _ => "gas-badge--plain"
    };
}

/// <summary>Sincroniza Stripe en segundo plano cada cierto tiempo (por defecto, cada hora).</summary>
public class SincronizadorStripe : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<OpcionesStripe> _monitor;
    private readonly ILogger<SincronizadorStripe> _logger;
    private OpcionesStripe _opciones => _monitor.CurrentValue; // llaves capturadas en pantalla, sin reiniciar

    public SincronizadorStripe(IServiceScopeFactory scopes, IOptionsMonitor<OpcionesStripe> opciones, ILogger<SincronizadorStripe> logger)
    {
        _scopes = scopes;
        _monitor = opciones;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.SincronizacionAutomatica) return;
        var intervalo = TimeSpan.FromMinutes(Math.Max(5, _opciones.MinutosEntreSincronizaciones));
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var servicio = scope.ServiceProvider.GetRequiredService<ServicioStripe>();
                    if (_opciones.Claves.Count > 0)
                    foreach (var r in await servicio.SincronizarTodasAsync(stoppingToken))
                        _logger.LogInformation("Stripe {Cuenta}: {Mensaje}", r.Cuenta, r.Mensaje);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error en la sincronización automática de Stripe.");
                }
                await Task.Delay(intervalo, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // La aplicación se está deteniendo.
        }
    }
}
