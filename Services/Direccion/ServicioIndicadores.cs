using System.Text.Json;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Finanzas;
using GAS.Admin.Services.Proyectos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GAS.Admin.Services.Direccion;

public enum AreaIndicador { Finanzas, Comercial, Operacion, Productos }

public enum FormatoIndicador { Dinero, Porcentaje, Numero, Horas }

/// <summary>
/// Definición de un indicador. <see cref="Puntual"/>: solo se conoce al momento (se guarda en el cierre);
/// los del periodo se pueden recalcular para cualquier mes. <see cref="MejorAlto"/>: null si no aplica.
/// </summary>
public sealed record Indicador(string Clave, string Nombre, AreaIndicador Area, FormatoIndicador Formato, bool? MejorAlto, bool Puntual, string Descripcion);

public sealed record ValoresMes(DateOnly Mes, Dictionary<string, decimal?> Valores, bool EnVivo, bool Parcial, DateTime? CapturadoUtc);

public class ServicioIndicadores
{
    public static readonly Indicador[] Todos =
    [
        // ---------- Finanzas ----------
        new("ingresos", "Ingresos cobrados", AreaIndicador.Finanzas, FormatoIndicador.Dinero, true, false, "Movimientos de ingreso confirmados en el mes, sin IVA, en pesos."),
        new("egresos", "Gastos pagados", AreaIndicador.Finanzas, FormatoIndicador.Dinero, false, false, "Egresos confirmados en el mes, sin IVA."),
        new("costoHoras", "Costo de horas", AreaIndicador.Finanzas, FormatoIndicador.Dinero, false, false, "Horas registradas × costo por hora de cada persona."),
        new("utilidad", "Utilidad", AreaIndicador.Finanzas, FormatoIndicador.Dinero, true, false, "Ingresos − gastos − costo de horas."),
        new("margen", "Margen", AreaIndicador.Finanzas, FormatoIndicador.Porcentaje, true, false, "Utilidad entre ingresos."),
        new("mrr", "MRR", AreaIndicador.Finanzas, FormatoIndicador.Dinero, true, true, "Ingreso recurrente mensual: servicios recurrentes + suscripciones de Stripe."),
        new("mrrStripe", "MRR de Stripe", AreaIndicador.Finanzas, FormatoIndicador.Dinero, true, true, "Suscripciones activas y con pago atrasado."),
        new("porCobrar", "Por cobrar", AreaIndicador.Finanzas, FormatoIndicador.Dinero, null, true, "Cobros pendientes con IVA."),
        new("porCobrarVencido", "Por cobrar vencido", AreaIndicador.Finanzas, FormatoIndicador.Dinero, false, true, "Cobros pendientes cuya fecha ya pasó."),
        new("flujo90", "Flujo neto a 90 días", AreaIndicador.Finanzas, FormatoIndicador.Dinero, true, true, "Entradas menos salidas esperadas en los próximos 3 meses."),

        // ---------- Comercial ----------
        new("oportunidadesAbiertas", "Oportunidades abiertas", AreaIndicador.Comercial, FormatoIndicador.Numero, true, true, "Oportunidades que no están ganadas ni perdidas."),
        new("embudoPonderado", "Embudo ponderado", AreaIndicador.Comercial, FormatoIndicador.Dinero, true, true, "Valor de las oportunidades abiertas × su probabilidad."),
        new("cotizacionesEnviadas", "Cotizaciones enviadas", AreaIndicador.Comercial, FormatoIndicador.Numero, true, false, "Cotizaciones enviadas en el mes."),
        new("cotizacionesAprobadas", "Cotizaciones aprobadas", AreaIndicador.Comercial, FormatoIndicador.Numero, true, false, "Cotizaciones aprobadas en el mes."),
        new("valorAprobado", "Valor aprobado", AreaIndicador.Comercial, FormatoIndicador.Dinero, true, false, "Subtotal de las cotizaciones aprobadas en el mes."),
        new("tasaCierre", "Tasa de cierre", AreaIndicador.Comercial, FormatoIndicador.Porcentaje, true, false, "Oportunidades ganadas entre ganadas + perdidas en el mes."),
        new("clientesActivos", "Clientes activos", AreaIndicador.Comercial, FormatoIndicador.Numero, true, true, "Clientes con estado Activo."),

        // ---------- Operación y equipo ----------
        new("trabajosActivos", "Trabajos activos", AreaIndicador.Operacion, FormatoIndicador.Numero, null, true, "Trabajos que no están cerrados ni cancelados."),
        new("trabajosAtrasados", "Trabajos atrasados", AreaIndicador.Operacion, FormatoIndicador.Numero, false, true, "Con fecha de entrega vencida."),
        new("trabajosCerrados", "Trabajos cerrados", AreaIndicador.Operacion, FormatoIndicador.Numero, true, false, "Cerrados en el mes."),
        new("horas", "Horas registradas", AreaIndicador.Operacion, FormatoIndicador.Horas, null, false, "Horas capturadas por el equipo en el mes."),
        new("utilizacion", "Utilización del equipo", AreaIndicador.Operacion, FormatoIndicador.Porcentaje, null, false, "Horas registradas entre la capacidad del mes (horas por semana de las personas activas)."),
        new("tareasVencidas", "Tareas vencidas", AreaIndicador.Operacion, FormatoIndicador.Numero, false, true, "Tareas sin terminar con vencimiento pasado."),

        // ---------- Productos, soporte e infraestructura ----------
        new("cuentasActivas", "Cuentas activas", AreaIndicador.Productos, FormatoIndicador.Numero, true, true, "Cuentas activas y en prueba de los productos conectados."),
        new("cuentasNuevas", "Cuentas nuevas", AreaIndicador.Productos, FormatoIndicador.Numero, true, false, "Cuentas creadas en el mes en los productos conectados."),
        new("usuariosActivos30d", "Usuarios activos (30 d)", AreaIndicador.Productos, FormatoIndicador.Numero, true, true, "Suma de lo que reporta cada producto."),
        new("suscripcionesActivas", "Suscripciones que pagan", AreaIndicador.Productos, FormatoIndicador.Numero, true, true, "Suscripciones de Stripe activas o con pago atrasado."),
        new("churn", "Churn de suscripciones", AreaIndicador.Productos, FormatoIndicador.Porcentaje, false, false, "Suscripciones canceladas en el mes entre las vigentes al inicio."),
        new("pagosFallidos", "Pagos fallidos", AreaIndicador.Productos, FormatoIndicador.Numero, false, true, "Facturas de Stripe con cobro fallido."),
        new("ticketsCreados", "Tickets recibidos", AreaIndicador.Productos, FormatoIndicador.Numero, null, false, "Tickets creados en el mes."),
        new("ticketsResueltos", "Tickets resueltos", AreaIndicador.Productos, FormatoIndicador.Numero, true, false, "Tickets resueltos en el mes."),
        new("slaPrimeraRespuesta", "Respuesta dentro del SLA", AreaIndicador.Productos, FormatoIndicador.Porcentaje, true, false, "Tickets del mes respondidos dentro del tiempo de su prioridad."),
        new("horasPrimeraRespuesta", "Primera respuesta promedio", AreaIndicador.Productos, FormatoIndicador.Horas, false, false, "Tiempo promedio a la primera respuesta en el mes."),
        new("incidentes", "Incidentes", AreaIndicador.Productos, FormatoIndicador.Numero, false, false, "Incidentes iniciados en el mes."),
        new("horasCaida", "Horas de caída", AreaIndicador.Productos, FormatoIndicador.Horas, false, false, "Duración de los incidentes críticos del mes."),
        new("disponibilidad", "Disponibilidad", AreaIndicador.Productos, FormatoIndicador.Porcentaje, true, true, "Promedio de disponibilidad a 30 días de los monitores de UptimeRobot."),
    ];

    public static readonly Dictionary<string, Indicador> PorClave = Todos.ToDictionary(i => i.Clave);

    private readonly GasDbContext _db;
    private readonly ReportesFinanzas _reportes;
    private readonly ServicioFinanzas _finanzas;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    public ServicioIndicadores(GasDbContext db, ReportesFinanzas reportes, ServicioFinanzas finanzas, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _db = db;
        _reportes = reportes;
        _finanzas = finanzas;
        _cache = cache;
    }

    /// <summary>Cálculo para mostrar en pantalla, en caché 2 minutos (son decenas de consultas).</summary>
    private async Task<Dictionary<string, decimal?>> CalcularEnCacheAsync(DateOnly mes, bool incluirPuntuales) =>
        (await _cache.GetOrCreateAsync($"GAS.Indicadores.{mes:yyyy-MM}.{incluirPuntuales}", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return CalcularAsync(mes, incluirPuntuales);
        }))!;

    public static DateOnly MesActual => ServicioTrabajos.InicioMes(ServicioFinanzas.Hoy);

    /// <summary>Calcula los indicadores de un mes. Los puntuales solo si se piden (valen "ahora").</summary>
    public async Task<Dictionary<string, decimal?>> CalcularAsync(DateOnly mes, bool incluirPuntuales)
    {
        var v = new Dictionary<string, decimal?>();
        var hoy = ServicioFinanzas.Hoy;
        var fin = mes.AddMonths(1).AddDays(-1);
        if (fin > hoy) fin = hoy;
        var desdeUtc = Fechas.ALocalUtc(mes.ToDateTime(TimeOnly.MinValue));
        var hastaUtc = Fechas.ALocalUtc(mes.AddMonths(1).ToDateTime(TimeOnly.MinValue));
        decimal? Div(decimal a, decimal b) => b == 0 ? null : Math.Round(a / b, 4);

        // ---------- Finanzas ----------
        var r = await _reportes.ResumenAsync(mes, fin, incluirTrabajos: false);
        v["ingresos"] = r.Ingresos;
        v["egresos"] = r.Egresos;
        v["costoHoras"] = r.CostoHoras;
        v["utilidad"] = r.Utilidad;
        v["margen"] = r.Margen;
        if (incluirPuntuales)
        {
            v["mrr"] = r.Mrr;
            v["mrrStripe"] = r.HayStripe ? r.MrrStripe : null;
            v["porCobrar"] = r.PorCobrar;
            v["porCobrarVencido"] = r.PorCobrarVencido;
            v["flujo90"] = r.Flujo.Sum(f => f.Neto);
        }

        // ---------- Comercial ----------
        v["cotizacionesEnviadas"] = await _db.Cotizaciones.CountAsync(c => c.FechaEnvioUtc >= desdeUtc && c.FechaEnvioUtc < hastaUtc);
        var aprobadas = await _db.Cotizaciones.AsNoTracking().Where(c => c.FechaAprobacionUtc >= desdeUtc && c.FechaAprobacionUtc < hastaUtc)
            .Select(c => new { c.Subtotal, c.Moneda }).ToListAsync();
        v["cotizacionesAprobadas"] = aprobadas.Count;
        decimal valor = 0;
        foreach (var a in aprobadas) valor += await _finanzas.AMxnAsync(a.Subtotal, a.Moneda) ?? 0;
        v["valorAprobado"] = valor;
        var cerradas = await _db.Oportunidades.AsNoTracking().Where(o => o.FechaCierreUtc >= desdeUtc && o.FechaCierreUtc < hastaUtc)
            .Select(o => o.Etapa).ToListAsync();
        var ganadas = cerradas.Count(e => e == EtapaOportunidad.Ganada);
        v["tasaCierre"] = Div(ganadas, cerradas.Count(e => e is EtapaOportunidad.Ganada or EtapaOportunidad.Perdida));
        if (incluirPuntuales)
        {
            var abiertas = await _db.Oportunidades.AsNoTracking().Where(o => o.Etapa != EtapaOportunidad.Ganada && o.Etapa != EtapaOportunidad.Perdida)
                .Select(o => new { o.ValorEstimado, o.Moneda, o.Probabilidad }).ToListAsync();
            v["oportunidadesAbiertas"] = abiertas.Count;
            decimal ponderado = 0;
            foreach (var o in abiertas.Where(o => o.ValorEstimado is not null))
                ponderado += (await _finanzas.AMxnAsync(o.ValorEstimado!.Value, o.Moneda) ?? 0) * o.Probabilidad / 100m;
            v["embudoPonderado"] = Math.Round(ponderado, 2);
            v["clientesActivos"] = await _db.Clientes.CountAsync(c => c.Estado == EstadoCliente.Activo);
        }

        // ---------- Operación y equipo ----------
        v["trabajosCerrados"] = await _db.Trabajos.CountAsync(t => t.Estado == EstadoTrabajo.Cerrado && t.FechaCierre >= mes && t.FechaCierre <= fin);
        var horas = await _db.RegistrosHoras.Where(h => h.Fecha >= mes && h.Fecha <= fin).SumAsync(h => (decimal?)h.Horas) ?? 0;
        v["horas"] = horas;
        var horasSemana = await _db.Personas.Where(p => p.Activa).SumAsync(p => (int?)p.HorasSemana) ?? 0;
        var dias = fin.DayNumber - mes.DayNumber + 1;
        v["utilizacion"] = Div(horas, Math.Round(horasSemana * dias / 7m, 2));
        if (incluirPuntuales)
        {
            v["trabajosActivos"] = await _db.Trabajos.CountAsync(t => t.Estado != EstadoTrabajo.Cerrado && t.Estado != EstadoTrabajo.Cancelado);
            v["trabajosAtrasados"] = await _db.Trabajos.CountAsync(t => t.Estado != EstadoTrabajo.Cerrado && t.Estado != EstadoTrabajo.Cancelado
                                                                        && t.Estado != EstadoTrabajo.EnPausa && t.FechaEntrega < hoy);
            v["tareasVencidas"] = await _db.Tareas.CountAsync(t => t.Estado != EstadoTarea.Terminado && t.FechaVencimiento < hoy);
        }

        // ---------- Productos, soporte e infraestructura ----------
        var hayConexiones = await _db.ConexionesProducto.AnyAsync();
        v["cuentasNuevas"] = hayConexiones ? await _db.CuentasProducto.CountAsync(c => c.CreadaUtc >= desdeUtc && c.CreadaUtc < hastaUtc) : null;
        var hayStripe = await _db.CuentasStripe.AnyAsync();
        if (hayStripe)
        {
            var subs = await _db.SuscripcionesStripe.AsNoTracking().Select(s => new { s.Estado, s.InicioUtc, s.CanceladaUtc }).ToListAsync();
            var canceladasMes = subs.Count(s => s.CanceladaUtc >= desdeUtc && s.CanceladaUtc < hastaUtc);
            var vigentesInicio = subs.Count(s => s.InicioUtc < desdeUtc && (s.CanceladaUtc is null || s.CanceladaUtc >= desdeUtc));
            v["churn"] = Div(canceladasMes, vigentesInicio);
        }
        var tickets = await _db.Tickets.AsNoTracking().Where(t => t.CreadoUtc >= desdeUtc && t.CreadoUtc < hastaUtc)
            .Select(t => new { t.CreadoUtc, t.PrimeraRespuestaUtc, t.VencePrimeraRespuestaUtc }).ToListAsync();
        v["ticketsCreados"] = tickets.Count;
        v["ticketsResueltos"] = await _db.Tickets.CountAsync(t => t.ResueltoUtc >= desdeUtc && t.ResueltoUtc < hastaUtc);
        var respondidos = tickets.Where(t => t.PrimeraRespuestaUtc is not null).ToList();
        var medibles = tickets.Where(t => t.VencePrimeraRespuestaUtc is not null && (t.PrimeraRespuestaUtc is not null || t.VencePrimeraRespuestaUtc < DateTime.UtcNow)).ToList();
        v["slaPrimeraRespuesta"] = Div(medibles.Count(t => t.PrimeraRespuestaUtc <= t.VencePrimeraRespuestaUtc), medibles.Count);
        v["horasPrimeraRespuesta"] = respondidos.Count == 0 ? null
            : Math.Round((decimal)respondidos.Average(t => (t.PrimeraRespuestaUtc!.Value - t.CreadoUtc).TotalHours), 2);
        var incidentes = await _db.Incidentes.AsNoTracking().Where(i => i.InicioUtc >= desdeUtc && i.InicioUtc < hastaUtc)
            .Select(i => new { i.Severidad, i.InicioUtc, i.FinUtc }).ToListAsync();
        v["incidentes"] = incidentes.Count;
        v["horasCaida"] = Math.Round((decimal)incidentes.Where(i => i.Severidad == SeveridadIncidente.Critica)
            .Sum(i => ((i.FinUtc ?? DateTime.UtcNow) - i.InicioUtc).TotalHours), 2);

        if (incluirPuntuales)
        {
            v["cuentasActivas"] = hayConexiones ? await _db.CuentasProducto.CountAsync(c => c.Estado == "activa" || c.Estado == "prueba") : null;
            if (hayConexiones)
            {
                var ultimas = await _db.MetricasProducto.AsNoTracking().GroupBy(m => m.ConexionProductoId)
                    .Select(g => g.OrderByDescending(m => m.Fecha).First().UsuariosActivos30d).ToListAsync();
                v["usuariosActivos30d"] = ultimas.Sum();
            }
            if (hayStripe)
            {
                v["suscripcionesActivas"] = await _db.SuscripcionesStripe.CountAsync(s => s.Estado == "active" || s.Estado == "past_due");
                v["pagosFallidos"] = await _db.FacturasStripe.CountAsync(f => (f.Estado == "open" && f.Intentos > 0) || f.Estado == "uncollectible");
            }
            var uptimes = await _db.Monitores.Where(m => m.Existe && m.Uptime30d != null).Select(m => m.Uptime30d!.Value).ToListAsync();
            v["disponibilidad"] = uptimes.Count == 0 ? null : Math.Round(uptimes.Average() / 100m, 5);
        }
        return v;
    }

    // ---------- Cierres ----------

    public async Task<ValoresMes> ValoresAsync(DateOnly mes)
    {
        if (mes >= MesActual) return new(mes, await CalcularEnCacheAsync(mes, true), true, false, null);
        var c = await _db.CierresMensuales.AsNoTracking().FirstOrDefaultAsync(x => x.Mes == mes);
        if (c is not null) return new(mes, Deserializar(c.IndicadoresJson), false, c.Parcial, c.CapturadoUtc);
        return new(mes, await CalcularEnCacheAsync(mes, false), false, true, null);
    }

    public async Task<List<CierreMensual>> HistoricoAsync(int meses = 12) =>
        (await _db.CierresMensuales.AsNoTracking().Where(c => c.Mes >= MesActual.AddMonths(-meses)).ToListAsync()).OrderBy(c => c.Mes).ToList();

    /// <summary>Guarda (o reemplaza) el cierre de un mes ya terminado.</summary>
    public async Task<CierreMensual> CerrarAsync(DateOnly mes, bool conPuntuales)
    {
        var valores = await CalcularAsync(mes, conPuntuales);
        var c = await _db.CierresMensuales.FirstOrDefaultAsync(x => x.Mes == mes);
        if (c is null) _db.CierresMensuales.Add(c = new CierreMensual { Mes = mes });
        c.IndicadoresJson = JsonSerializer.Serialize(valores);
        c.Parcial = !conPuntuales;
        c.CapturadoUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return c;
    }

    /// <summary>Cierra el mes anterior si aún no tiene cierre (con los puntuales de este momento).</summary>
    public async Task<bool> CerrarMesAnteriorSiFaltaAsync()
    {
        var anterior = MesActual.AddMonths(-1);
        if (await _db.CierresMensuales.AnyAsync(c => c.Mes == anterior)) return false;
        await CerrarAsync(anterior, true);
        return true;
    }

    /// <summary>Reconstruye los meses sin cierre (solo indicadores del periodo). Devuelve cuántos creó.</summary>
    public async Task<int> ReconstruirAsync(int meses = 12)
    {
        var existentes = await _db.CierresMensuales.Select(c => c.Mes).ToListAsync();
        var n = 0;
        for (var i = meses; i >= 1; i--)
        {
            var mes = MesActual.AddMonths(-i);
            if (existentes.Contains(mes)) continue;
            await CerrarAsync(mes, false);
            n++;
        }
        return n;
    }

    public static Dictionary<string, decimal?> Deserializar(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, decimal?>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}

/// <summary>Cierra el mes anterior automáticamente (revisa al iniciar y cada 6 horas).</summary>
public class CierreMensualAutomatico : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CierreMensualAutomatico> _logger;

    public CierreMensualAutomatico(IServiceScopeFactory scopes, ILogger<CierreMensualAutomatico> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var servicio = scope.ServiceProvider.GetRequiredService<ServicioIndicadores>();
                    if (await servicio.CerrarMesAnteriorSiFaltaAsync())
                        _logger.LogInformation("Cierre mensual de indicadores guardado.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error al guardar el cierre mensual de indicadores.");
                }
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // La aplicación se está deteniendo.
        }
    }
}
