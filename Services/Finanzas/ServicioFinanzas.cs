using System.Globalization;
using GAS.Admin.Data.Context;
using GAS.Admin.Data.Seed;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Finanzas;

/// <summary>Renovación de un servicio externo o de un dominio que todavía no tiene cargo esperado.</summary>
public sealed record RenovacionPendiente(string Nombre, DateOnly Fecha, ServicioExterno? Servicio = null, Dominio? Dominio = null);

/// <summary>
/// Reglas de Finanzas: cobros esperados desde cotizaciones y ciclos, cargos de renovación
/// de servicios externos y conversión a pesos.
/// </summary>
public class ServicioFinanzas
{
    public static readonly string[] Monedas = ["MXN", "USD"];
    private static readonly CultureInfo Mx = new("es-MX");

    private readonly GasDbContext _db;
    private readonly Dictionary<string, decimal?> _tipos = [];

    public ServicioFinanzas(GasDbContext db) => _db = db;

    public static DateOnly Hoy => DateOnly.FromDateTime(Fechas.ALocal(DateTime.UtcNow));

    // ---------- Tipo de cambio ----------

    /// <summary>Último tipo de cambio registrado en o antes de la fecha (o el más reciente). 1 para MXN.</summary>
    public async Task<decimal?> TipoCambioAsync(string moneda, DateOnly? fecha = null)
    {
        if (moneda == "MXN") return 1;
        var clave = $"{moneda}|{fecha}";
        if (_tipos.TryGetValue(clave, out var cache)) return cache;

        var q = _db.TiposCambio.AsNoTracking().Where(t => t.Moneda == moneda);
        decimal? valor = null;
        if (fecha is not null)
        {
            valor = await q.Where(t => t.Fecha <= fecha).OrderByDescending(t => t.Fecha).Select(t => (decimal?)t.Valor).FirstOrDefaultAsync();
        }
        valor ??= await q.OrderByDescending(t => t.Fecha).Select(t => (decimal?)t.Valor).FirstOrDefaultAsync();
        _tipos[clave] = valor;
        return valor;
    }

    /// <summary>Convierte a pesos con el tipo de cambio más reciente; null si no hay tipo de cambio.</summary>
    public async Task<decimal?> AMxnAsync(decimal? monto, string moneda, DateOnly? fecha = null)
    {
        if (monto is null) return null;
        var tc = await TipoCambioAsync(moneda, fecha);
        return tc is null ? null : Math.Round(monto.Value * tc.Value, 2);
    }

    private async Task<int?> CategoriaAsync(string nombre, TipoMovimiento tipo) =>
        await _db.CategoriasFinancieras.Where(c => c.Nombre == nombre && c.Tipo == tipo).Select(c => (int?)c.Id).FirstOrDefaultAsync();

    // ---------- Cobros desde cotizaciones ----------

    /// <summary>
    /// Agrega (sin guardar) los cobros esperados de una cotización aprobada según su condición de pago.
    /// Un servicio recurrente cobra por ciclo mensual.
    /// </summary>
    public async Task<List<Movimiento>> GenerarCobrosDeCotizacionAsync(Cotizacion c, Trabajo t)
    {
        if (c.Id != 0 && await _db.Movimientos.AnyAsync(m => m.CotizacionId == c.Id)) return [];

        var hoy = Hoy;
        var tc = await TipoCambioAsync(c.Moneda, hoy) ?? 0;
        var recurrente = t.Tipo == TipoTrabajoOperativo.ServicioRecurrente;
        var categoria = await CategoriaAsync(recurrente ? FinanzasSeeder.Categorias.Recurrentes : FinanzasSeeder.Categorias.Desarrollo, TipoMovimiento.Ingreso);

        Movimiento Cobro(string concepto, decimal subtotal, DateOnly fecha) => new()
        {
            Tipo = TipoMovimiento.Ingreso,
            Concepto = Recortar($"{concepto} · {c.Titulo}"),
            CategoriaId = categoria,
            Linea = LineaNegocio.Servicios,
            Origen = OrigenMovimiento.Cotizacion,
            FechaProgramada = fecha,
            Moneda = c.Moneda,
            Subtotal = subtotal,
            TasaIva = c.TasaIva,
            TipoCambio = tc,
            ClienteId = c.ClienteId,
            ProductoId = t.ProductoId,
            Trabajo = t,
            Cotizacion = c,
            Referencia = c.FolioCompleto
        };

        var cobros = new List<Movimiento>();
        if (recurrente)
        {
            foreach (var ciclo in t.Ciclos)
            {
                var m = Cobro($"Mensualidad {ciclo.Periodo.ToString("MMMM yyyy", Mx)}", ciclo.Monto ?? c.Subtotal, ciclo.Periodo < hoy ? hoy : ciclo.Periodo);
                m.Ciclo = ciclo;
                m.Origen = OrigenMovimiento.CicloRecurrente;
                cobros.Add(m);
            }
        }
        else
        {
            var entregables = t.Entregables.OrderBy(e => e.Orden).ToList();
            var final = t.FechaEntrega ?? hoy.AddMonths(1);
            switch (c.CondicionPago)
            {
                case CondicionPago.AnticipoYFinal when c.AnticipoPorcentaje is > 0 and < 100:
                    var partes = Partes(c.Subtotal, [c.AnticipoPorcentaje.Value, 100 - c.AnticipoPorcentaje.Value]);
                    cobros.Add(Cobro($"Anticipo {c.AnticipoPorcentaje}%", partes[0], hoy));
                    var finiquito = Cobro("Finiquito", partes[1], final);
                    finiquito.Entregable = entregables.LastOrDefault();
                    cobros.Add(finiquito);
                    break;

                case CondicionPago.Hitos when entregables.Count > 1:
                    var lineas = c.Lineas.OrderBy(l => l.Orden).ToList();
                    var pesos = lineas.Count == entregables.Count ? lineas.Select(l => l.Importe).ToArray() : entregables.Select(_ => 1m).ToArray();
                    var montos = Partes(c.Subtotal, pesos);
                    for (var i = 0; i < entregables.Count; i++)
                    {
                        var e = entregables[i];
                        var m = Cobro($"Hito {i + 1}: {e.Nombre}", montos[i], e.FechaCompromiso ?? hoy.AddMonths(i + 1));
                        m.Entregable = e;
                        cobros.Add(m);
                    }
                    break;

                default:
                    cobros.Add(Cobro(c.CondicionPago == CondicionPago.Contado ? "Pago de contado" : "Pago único", c.Subtotal, hoy));
                    break;
            }
        }

        cobros.ForEach(m => m.Recalcular());
        // Si la cotización se dividió en partes, el último cobro absorbe el redondeo del IVA
        // para que la suma sea exactamente el total cotizado.
        if (!recurrente && cobros.Count > 1)
        {
            var ultimo = cobros[^1];
            ultimo.Iva = c.Iva - cobros.Take(cobros.Count - 1).Sum(m => m.Iva);
            ultimo.Total = ultimo.Subtotal + ultimo.Iva;
            ultimo.TotalMxn = Math.Round(ultimo.Total * ultimo.TipoCambio, 2);
        }
        _db.Movimientos.AddRange(cobros);
        return cobros;
    }

    /// <summary>Agrega (sin guardar) el cobro esperado de un ciclo mensual si el trabajo tiene monto.</summary>
    public async Task<Movimiento?> GenerarCobroDeCicloAsync(Trabajo t, CicloTrabajo ciclo)
    {
        var monto = ciclo.Monto ?? t.Presupuesto;
        if (monto is null or <= 0) return null;
        if (ciclo.Id != 0 && await _db.Movimientos.AnyAsync(m => m.CicloTrabajoId == ciclo.Id)) return null;

        var hoy = Hoy;
        var tasa = t.CotizacionId is null ? 0.16m
            : await _db.Cotizaciones.Where(c => c.Id == t.CotizacionId).Select(c => c.TasaIva).FirstOrDefaultAsync();
        var m = new Movimiento
        {
            Tipo = TipoMovimiento.Ingreso,
            Concepto = Recortar($"Mensualidad {ciclo.Periodo.ToString("MMMM yyyy", Mx)} · {t.Nombre}"),
            CategoriaId = await CategoriaAsync(FinanzasSeeder.Categorias.Recurrentes, TipoMovimiento.Ingreso),
            Linea = LineaNegocio.Servicios,
            Origen = OrigenMovimiento.CicloRecurrente,
            FechaProgramada = ciclo.Periodo < hoy ? hoy : ciclo.Periodo,
            Moneda = t.Moneda,
            Subtotal = monto.Value,
            TasaIva = tasa,
            TipoCambio = await TipoCambioAsync(t.Moneda, hoy) ?? 0,
            ClienteId = t.ClienteId,
            ProductoId = t.ProductoId,
            Trabajo = t,
            Ciclo = ciclo,
            CotizacionId = t.CotizacionId,
            Referencia = t.Folio
        };
        m.Recalcular();
        _db.Movimientos.Add(m);
        return m;
    }

    // ---------- Renovaciones de servicios externos ----------

    /// <summary>Siguiente renovación respetando el día original (si el mes es más corto, el último día).</summary>
    public static DateOnly? SiguienteRenovacion(DateOnly fecha, PeriodicidadCobro p, int? dia)
    {
        if (SiguienteRenovacion(fecha, p) is not { } s || dia is null) return SiguienteRenovacion(fecha, p);
        return new DateOnly(s.Year, s.Month, Math.Min(dia.Value, DateTime.DaysInMonth(s.Year, s.Month)));
    }

    public static DateOnly? SiguienteRenovacion(DateOnly fecha, PeriodicidadCobro p) => p switch
    {
        PeriodicidadCobro.Mensual => fecha.AddMonths(1),
        PeriodicidadCobro.Trimestral => fecha.AddMonths(3),
        PeriodicidadCobro.Anual => fecha.AddYears(1),
        _ => null
    };

    private static bool Renovable(ServicioExterno s) =>
        s.Estado != EstadoServicio.Cancelado && s.Costo is > 0 && s.ProximaRenovacion is not null
        && s.Periodicidad is PeriodicidadCobro.Mensual or PeriodicidadCobro.Trimestral or PeriodicidadCobro.Anual or PeriodicidadCobro.Unico;

    /// <summary>Renovaciones (servicios externos y dominios) dentro del horizonte que aún no tienen un cargo esperado.</summary>
    public async Task<List<RenovacionPendiente>> RenovacionesSinCargoAsync(int dias = 30)
    {
        var limite = Hoy.AddDays(dias);
        var servicios = (await _db.ServiciosExternos.AsNoTracking().Include(s => s.Productos)
                .Where(s => s.Estado != EstadoServicio.Cancelado && s.ProximaRenovacion != null && s.ProximaRenovacion <= limite)
                .ToListAsync())
            .Where(Renovable).ToList();
        var dominios = await _db.Dominios.AsNoTracking()
            .Where(d => d.Estado == EstadoDominio.Activo && d.Costo > 0 && d.FechaVencimiento != null && d.FechaVencimiento <= limite)
            .ToListAsync();
        if (servicios.Count == 0 && dominios.Count == 0) return [];

        var ids = servicios.Select(s => s.Id).ToList();
        // Se compara por mes: un cargo reprogramado unos días no debe generar otro.
        var existentes = (await _db.Movimientos.AsNoTracking().Where(m => m.ServicioExternoId != null && ids.Contains(m.ServicioExternoId.Value))
                .Select(m => new { m.ServicioExternoId, m.FechaProgramada }).ToListAsync())
            .Select(m => (m.ServicioExternoId!.Value, Periodo(m.FechaProgramada))).ToHashSet();
        var idsDom = dominios.Select(d => d.Id).ToList();
        var existentesDom = (await _db.Movimientos.AsNoTracking().Where(m => m.DominioId != null && idsDom.Contains(m.DominioId.Value))
                .Select(m => new { m.DominioId, m.FechaProgramada }).ToListAsync())
            .Select(m => (m.DominioId!.Value, Periodo(m.FechaProgramada))).ToHashSet();

        return servicios.Where(s => !existentes.Contains((s.Id, Periodo(s.ProximaRenovacion!.Value))))
            .Select(s => new RenovacionPendiente(s.Nombre, s.ProximaRenovacion!.Value, Servicio: s))
            .Concat(dominios.Where(d => !existentesDom.Contains((d.Id, Periodo(d.FechaVencimiento!.Value))))
                .Select(d => new RenovacionPendiente(d.Nombre, d.FechaVencimiento!.Value, Dominio: d)))
            .OrderBy(r => r.Fecha).ToList();
    }

    /// <summary>Crea y guarda los cargos esperados de las renovaciones próximas. Devuelve cuántos creó.</summary>
    public async Task<int> GenerarCargosRenovacionAsync(int dias = 30)
    {
        var pendientes = await RenovacionesSinCargoAsync(dias);
        if (pendientes.Count == 0) return 0;

        var software = await CategoriaAsync(FinanzasSeeder.Categorias.Software, TipoMovimiento.Egreso);
        var dominios = await CategoriaAsync(FinanzasSeeder.Categorias.Dominios, TipoMovimiento.Egreso);
        foreach (var r in pendientes)
        {
            Movimiento m;
            if (r.Servicio is { } s)
            {
                m = new Movimiento
                {
                    Concepto = Recortar($"{s.Nombre}{(s.Plan is null ? "" : " · " + s.Plan)} · {Etiquetas.Texto(s.Periodicidad).ToLowerInvariant()}"),
                    CategoriaId = s.Categoria is CategoriaServicio.Dominios or CategoriaServicio.Hosting ? dominios : software,
                    Linea = s.Productos.Count > 0 ? LineaNegocio.ProductosSaaS : LineaNegocio.Interno,
                    Moneda = s.Moneda,
                    Subtotal = s.Costo!.Value,
                    ServicioExternoId = s.Id,
                    Proveedor = s.Nombre,
                    ProductoId = s.Productos.Count == 1 ? s.Productos[0].ProductoId : null
                };
            }
            else
            {
                var d = r.Dominio!;
                var registrador = d.RegistradorId is null ? null
                    : await _db.ServiciosExternos.Where(x => x.Id == d.RegistradorId).Select(x => x.Nombre).FirstOrDefaultAsync();
                m = new Movimiento
                {
                    Concepto = Recortar($"Dominio {d.Nombre} · renovación {d.AniosRenovacion} año{(d.AniosRenovacion == 1 ? "" : "s")}"),
                    CategoriaId = dominios,
                    Linea = d.ProductoId is null ? LineaNegocio.Interno : LineaNegocio.ProductosSaaS,
                    Moneda = d.Moneda,
                    Subtotal = d.Costo!.Value,
                    DominioId = d.Id,
                    Proveedor = registrador,
                    ProductoId = d.ProductoId
                };
            }
            m.Tipo = TipoMovimiento.Egreso;
            m.Origen = OrigenMovimiento.Renovacion;
            m.FechaProgramada = r.Fecha;
            m.TasaIva = 0;
            m.TipoCambio = await TipoCambioAsync(m.Moneda, Hoy) ?? 0;
            m.Recalcular();
            _db.Movimientos.Add(m);
        }
        await _db.SaveChangesAsync();
        return pendientes.Count;
    }

    /// <summary>
    /// Al confirmar el pago de una renovación, mueve la próxima renovación del servicio
    /// al siguiente periodo (si aún apunta a la fecha pagada).
    /// </summary>
    public async Task<string?> AlConfirmarAsync(Movimiento m)
    {
        if (m.Origen != OrigenMovimiento.Renovacion) return null;
        if (m.DominioId is not null)
        {
            var d = await _db.Dominios.FirstOrDefaultAsync(x => x.Id == m.DominioId);
            if (d?.FechaVencimiento is not { } v || Periodo(v) != Periodo(m.FechaProgramada)) return null;
            d.FechaVencimiento = v.AddYears(Math.Max(1, d.AniosRenovacion));
            return $"{d.Nombre} ahora vence el {d.FechaVencimiento:dd/MM/yyyy}.";
        }
        if (m.ServicioExternoId is null) return null;
        var s = await _db.ServiciosExternos.FirstOrDefaultAsync(x => x.Id == m.ServicioExternoId);
        // Se avanza desde la fecha del servicio (no la del cargo) para que no se recorra el día.
        if (s?.ProximaRenovacion is not { } p || Periodo(p) != Periodo(m.FechaProgramada)) return null;
        s.DiaRenovacion ??= p.Day;
        s.ProximaRenovacion = SiguienteRenovacion(p, s.Periodicidad, s.DiaRenovacion);
        return s.ProximaRenovacion is null
            ? $"{s.Nombre} ya no tiene próxima renovación."
            : $"Próxima renovación de {s.Nombre}: {s.ProximaRenovacion:dd/MM/yyyy}.";
    }

    /// <summary>
    /// Si un pago de renovación ya confirmado vuelve a pendiente o se cancela, regresa la próxima
    /// renovación (o el vencimiento del dominio) al periodo de ese cargo.
    /// </summary>
    public async Task<string?> AlRevertirAsync(Movimiento m)
    {
        if (m.Origen != OrigenMovimiento.Renovacion) return null;
        if (m.DominioId is not null)
        {
            var d = await _db.Dominios.FirstOrDefaultAsync(x => x.Id == m.DominioId);
            if (d?.FechaVencimiento is not { } v) return null;
            var anterior = v.AddYears(-Math.Max(1, d.AniosRenovacion));
            if (Periodo(anterior) != Periodo(m.FechaProgramada)) return null;
            d.FechaVencimiento = anterior;
            return $"{d.Nombre} vuelve a vencer el {anterior:dd/MM/yyyy}.";
        }
        if (m.ServicioExternoId is null) return null;
        var s = await _db.ServiciosExternos.FirstOrDefaultAsync(x => x.Id == m.ServicioExternoId);
        if (s is null) return null;
        DateOnly? previa = s.ProximaRenovacion is null
            ? (s.Periodicidad == PeriodicidadCobro.Unico ? m.FechaProgramada : null)
            : s.Periodicidad switch
            {
                PeriodicidadCobro.Mensual => s.ProximaRenovacion.Value.AddMonths(-1),
                PeriodicidadCobro.Trimestral => s.ProximaRenovacion.Value.AddMonths(-3),
                PeriodicidadCobro.Anual => s.ProximaRenovacion.Value.AddYears(-1),
                _ => null
            };
        if (previa is { } pv && s.DiaRenovacion is { } dia)
            previa = new DateOnly(pv.Year, pv.Month, Math.Min(dia, DateTime.DaysInMonth(pv.Year, pv.Month)));
        if (previa is null || Periodo(previa.Value) != Periodo(m.FechaProgramada)) return null;
        s.ProximaRenovacion = previa;
        return $"La próxima renovación de {s.Nombre} vuelve a ser el {previa:dd/MM/yyyy}.";
    }

    private static (int, int) Periodo(DateOnly f) => (f.Year, f.Month);

    // ---------- Apoyo ----------

    /// <summary>Reparte un monto según pesos, sin perder centavos (el último absorbe el redondeo).</summary>
    public static decimal[] Partes(decimal total, decimal[] pesos)
    {
        var suma = pesos.Sum();
        if (suma <= 0) pesos = pesos.Select(_ => 1m).ToArray();
        suma = pesos.Sum();
        var r = new decimal[pesos.Length];
        decimal acumulado = 0;
        for (var i = 0; i < pesos.Length - 1; i++)
        {
            r[i] = Math.Round(total * pesos[i] / suma, 2);
            acumulado += r[i];
        }
        r[^1] = total - acumulado;
        return r;
    }

    private static string Recortar(string s) => s.Length > 200 ? s[..200] : s;
}
