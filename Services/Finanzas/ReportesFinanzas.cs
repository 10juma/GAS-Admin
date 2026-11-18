using System.Globalization;
using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Proyectos;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Finanzas;

/// <summary>Resultado de un producto (o de "General") en un periodo, en pesos sin IVA.</summary>
public sealed class ResultadoProducto
{
    public string Nombre { get; init; } = string.Empty;
    public int? ProductoId { get; init; }
    public decimal Ingresos { get; set; }
    public decimal GastosDirectos { get; set; }
    public decimal CostosCompartidos { get; set; }
    public decimal Horas { get; set; }
    public decimal CostoHoras { get; set; }
    public decimal Costos => GastosDirectos + CostosCompartidos + CostoHoras;
    public decimal Utilidad => Ingresos - Costos;
    public decimal? Margen => Ingresos > 0 ? Math.Round(Utilidad / Ingresos, 4) : null;
    public bool Vacio => Ingresos == 0 && Costos == 0;
}

public sealed record ResultadoLinea(LineaNegocio Linea, decimal Ingresos, decimal Egresos)
{
    public decimal Utilidad => Ingresos - Egresos;
}

/// <summary>Un mes del flujo proyectado (con IVA, en pesos).</summary>
public sealed record MesFlujo(DateOnly Mes, decimal Entradas, decimal Salidas, decimal EntradasProyectadas, decimal SalidasProyectadas)
{
    public decimal TotalEntradas => Entradas + EntradasProyectadas;
    public decimal TotalSalidas => Salidas + SalidasProyectadas;
    public decimal Neto => TotalEntradas - TotalSalidas;
}

public sealed record MargenTrabajo(Trabajo Trabajo, string? Cliente, Rentabilidad Rentabilidad)
{
    public bool BajoEstimado => Rentabilidad.Margen is not null && Rentabilidad.MargenEstimado is not null
                                && Rentabilidad.Margen < Rentabilidad.MargenEstimado;
}

public sealed class ResumenFinanzas
{
    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }
    public decimal Ingresos { get; init; }
    public decimal Egresos { get; init; }
    public decimal Horas { get; init; }
    public decimal CostoHoras { get; init; }
    public decimal UtilidadOperativa => Ingresos - Egresos;
    public decimal Utilidad => Ingresos - Egresos - CostoHoras;
    public decimal? Margen => Ingresos > 0 ? Math.Round(Utilidad / Ingresos, 4) : null;
    public decimal PorCobrar { get; init; }
    public decimal PorCobrarVencido { get; init; }
    public decimal PorPagar { get; init; }
    public decimal PorPagarVencido { get; init; }
    public decimal Mrr => MrrServicios + MrrStripe;
    public decimal MrrServicios { get; init; }
    public decimal MrrStripe { get; init; }
    public int SuscripcionesStripe { get; init; }
    public bool HayStripe { get; init; }
    public int Recurrentes { get; init; }
    public decimal CostoFijoMensual { get; init; }
    public decimal? TipoCambio { get; init; }
    public bool FaltaTipoCambio { get; init; }
    public List<MesFlujo> Flujo { get; init; } = [];
    public List<ResultadoProducto> Productos { get; init; } = [];
    public List<ResultadoLinea> Lineas { get; init; } = [];
    public List<MargenTrabajo> Trabajos { get; init; } = [];
    public List<ServicioExterno> Renovaciones { get; init; } = [];
    public List<Dominio> DominiosPorVencer { get; init; } = [];
}

public class ReportesFinanzas
{
    private readonly GasDbContext _db;
    private readonly ServicioFinanzas _finanzas;
    private readonly ServicioTrabajos _trabajos;

    public ReportesFinanzas(GasDbContext db, ServicioFinanzas finanzas, ServicioTrabajos trabajos)
    {
        _db = db;
        _finanzas = finanzas;
        _trabajos = trabajos;
    }

    public async Task<ResumenFinanzas> ResumenAsync(DateOnly desde, DateOnly hasta, bool incluirTrabajos = true)
    {
        var hoy = ServicioFinanzas.Hoy;
        var usd = await _finanzas.TipoCambioAsync("USD", hoy);
        var faltaTc = false;
        decimal Mxn(decimal monto, string moneda)
        {
            if (moneda == "MXN") return monto;
            if (usd is null) { faltaTc = true; return 0; }
            return Math.Round(monto * usd.Value, 2);
        }

        // ---------- Realizado en el periodo (sin IVA) ----------
        var confirmados = await _db.Movimientos.AsNoTracking()
            .Where(m => m.Estado == EstadoMovimiento.Confirmado && m.FechaPago >= desde && m.FechaPago <= hasta)
            .Select(m => new
            {
                m.Tipo, m.Linea, m.SubtotalMxn, m.ProductoId, m.ServicioExternoId,
                ProductoTrabajo = m.Trabajo == null ? null : m.Trabajo.ProductoId
            })
            .ToListAsync();

        var horas = await _db.RegistrosHoras.AsNoTracking()
            .Where(h => h.Fecha >= desde && h.Fecha <= hasta)
            .Select(h => new
            {
                h.Horas, h.CostoHora, h.MonedaCosto,
                ProductoId = h.Trabajo == null ? null : h.Trabajo.ProductoId,
                Interno = h.Trabajo != null && h.Trabajo.Tipo == TipoTrabajoOperativo.ProyectoInterno
            })
            .ToListAsync();
        decimal CostoHora(decimal h, decimal? costo, string? moneda) => costo is null ? 0 : Mxn(Math.Round(h * costo.Value, 2), moneda ?? "MXN");

        // ---------- Pendientes ----------
        var pendientes = await _db.Movimientos.AsNoTracking().Where(m => m.Estado == EstadoMovimiento.Esperado)
            .Select(m => new { m.Tipo, m.TotalMxn, m.FechaProgramada, m.CicloTrabajoId, Periodo = m.Ciclo == null ? (DateOnly?)null : m.Ciclo.Periodo, m.TrabajoId })
            .ToListAsync();

        // ---------- Recurrentes (MRR) y costo fijo ----------
        var recurrentes = await _db.Trabajos.AsNoTracking()
            .Where(t => t.Tipo == TipoTrabajoOperativo.ServicioRecurrente && t.Estado != EstadoTrabajo.Cerrado && t.Estado != EstadoTrabajo.Cancelado && t.Presupuesto != null)
            .Select(t => new { t.Id, t.Presupuesto, t.Moneda, Tasa = t.Cotizacion == null ? 0.16m : t.Cotizacion.TasaIva, Periodos = t.Ciclos.Select(c => c.Periodo).ToList() })
            .ToListAsync();
        var mrr = recurrentes.Sum(t => Mxn(t.Presupuesto!.Value, t.Moneda));
        var stripe = await _db.SuscripcionesStripe.AsNoTracking().Where(s => s.Estado == "active" || s.Estado == "past_due")
            .Select(s => new { s.MontoMensual, s.Moneda }).ToListAsync();
        var mrrStripe = stripe.Sum(s => Mxn(s.MontoMensual, s.Moneda));
        var hayStripe = await _db.CuentasStripe.AnyAsync();

        var servicios = await _db.ServiciosExternos.AsNoTracking().Include(s => s.Productos).ThenInclude(p => p.Producto)
            .Where(s => s.Estado != EstadoServicio.Cancelado).ToListAsync();
        var dominiosActivos = await _db.Dominios.AsNoTracking().Include(d => d.Producto)
            .Where(d => d.Estado == EstadoDominio.Activo).ToListAsync();
        var costoFijo = servicios.Sum(s => s.CostoMensual is null ? 0 : Mxn(s.CostoMensual.Value, s.Moneda))
                        + dominiosActivos.Sum(d => d.CostoMensual is null ? 0 : Mxn(d.CostoMensual.Value, d.Moneda));

        // ---------- Flujo proyectado 90 días (con IVA) ----------
        var inicio = ServicioTrabajos.InicioMes(hoy);
        var meses = Enumerable.Range(0, 3).Select(i => inicio.AddMonths(i)).ToList();
        var limite = inicio.AddMonths(3);
        DateOnly MesDe(DateOnly f) => f < inicio ? inicio : ServicioTrabajos.InicioMes(f);

        var renovacionesGeneradas = (await _db.Movimientos.AsNoTracking()
                .Where(m => m.ServicioExternoId != null && m.FechaProgramada >= inicio)
                .Select(m => new { m.ServicioExternoId, m.FechaProgramada }).ToListAsync())
            .Select(m => (m.ServicioExternoId!.Value, m.FechaProgramada)).ToHashSet();
        var dominiosGenerados = (await _db.Movimientos.AsNoTracking()
                .Where(m => m.DominioId != null && m.FechaProgramada >= inicio)
                .Select(m => new { m.DominioId, m.FechaProgramada }).ToListAsync())
            .Select(m => (m.DominioId!.Value, m.FechaProgramada)).ToHashSet();

        var flujo = meses.Select(mes =>
        {
            var fin = mes.AddMonths(1);
            var entradas = pendientes.Where(p => p.Tipo == TipoMovimiento.Ingreso && MesDe(p.FechaProgramada) == mes).Sum(p => p.TotalMxn);
            var salidas = pendientes.Where(p => p.Tipo == TipoMovimiento.Egreso && MesDe(p.FechaProgramada) == mes).Sum(p => p.TotalMxn);

            // Mensualidades de recurrentes cuyo ciclo de ese mes aún no existe.
            var entradasProy = recurrentes.Where(t => !t.Periodos.Contains(mes) && mes >= inicio)
                .Sum(t => Mxn(Math.Round(t.Presupuesto!.Value * (1 + t.Tasa), 2), t.Moneda));

            // Renovaciones de servicios externos aún sin cargo registrado.
            decimal salidasProy = 0;
            foreach (var s in servicios.Where(s => s.Costo is > 0 && s.ProximaRenovacion is not null
                                                   && s.Periodicidad is not (PeriodicidadCobro.PorUso or PeriodicidadCobro.Gratuito)))
            {
                DateOnly? f = s.ProximaRenovacion;
                var vueltas = 0;
                while (f is not null && f < fin && vueltas++ < 40)
                {
                    // Una renovación ya vencida cuenta una sola vez (en el mes actual).
                    var cuenta = f >= inicio || vueltas == 1;
                    if (cuenta && MesDe(f.Value) == mes && !renovacionesGeneradas.Contains((s.Id, f.Value)))
                        salidasProy += Mxn(s.Costo!.Value, s.Moneda);
                    f = ServicioFinanzas.SiguienteRenovacion(f.Value, s.Periodicidad, s.DiaRenovacion);
                }
            }
            salidasProy += dominiosActivos
                .Where(d => d.Costo is > 0 && d.FechaVencimiento is not null && MesDe(d.FechaVencimiento.Value) == mes
                            && !dominiosGenerados.Contains((d.Id, d.FechaVencimiento.Value)))
                .Sum(d => Mxn(d.Costo!.Value, d.Moneda));
            return new MesFlujo(mes, entradas, salidas, entradasProy, salidasProy);
        }).ToList();

        // ---------- Rentabilidad por producto ----------
        var productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new { p.Id, p.Nombre }).ToListAsync();
        var porProducto = productos.ToDictionary(p => p.Id, p => new ResultadoProducto { ProductoId = p.Id, Nombre = p.Nombre });
        var general = new ResultadoProducto { Nombre = "General (sin producto)" };
        ResultadoProducto De(int? id) => id is { } k && porProducto.TryGetValue(k, out var r) ? r : general;
        var productosDeServicio = servicios.ToDictionary(s => s.Id, s => s.Productos.Select(p => p.ProductoId).ToList());

        foreach (var m in confirmados)
        {
            var producto = m.ProductoId ?? m.ProductoTrabajo;
            if (m.Tipo == TipoMovimiento.Ingreso)
            {
                De(producto).Ingresos += m.SubtotalMxn;
            }
            else if (producto is null && m.ServicioExternoId is { } sid && productosDeServicio.TryGetValue(sid, out var usan) && usan.Count > 0)
            {
                // Costo compartido: partes iguales entre los productos que usan el servicio.
                var partes = ServicioFinanzas.Partes(m.SubtotalMxn, usan.Select(_ => 1m).ToArray());
                for (var i = 0; i < usan.Count; i++) De(usan[i]).CostosCompartidos += partes[i];
            }
            else
            {
                De(producto).GastosDirectos += m.SubtotalMxn;
            }
        }
        foreach (var h in horas.Where(h => h.ProductoId is not null))
        {
            var r = De(h.ProductoId);
            r.Horas += h.Horas;
            r.CostoHoras += CostoHora(h.Horas, h.CostoHora, h.MonedaCosto);
        }
        var sinProducto = horas.Where(h => h.ProductoId is null).ToList();
        general.Horas += sinProducto.Sum(h => h.Horas);
        general.CostoHoras += sinProducto.Sum(h => CostoHora(h.Horas, h.CostoHora, h.MonedaCosto));

        var filasProducto = porProducto.Values.Append(general).ToList();

        // ---------- Por línea de negocio ----------
        var lineas = Enum.GetValues<LineaNegocio>().Select(l => new ResultadoLinea(l,
            confirmados.Where(m => m.Linea == l && m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.SubtotalMxn),
            confirmados.Where(m => m.Linea == l && m.Tipo == TipoMovimiento.Egreso).Sum(m => m.SubtotalMxn))).ToList();

        // ---------- Margen por trabajo (activos con presupuesto) ----------
        var activos = !incluirTrabajos ? [] : await _db.Trabajos.AsNoTracking()
            .Where(t => t.Estado != EstadoTrabajo.Cerrado && t.Estado != EstadoTrabajo.Cancelado
                        && t.Tipo != TipoTrabajoOperativo.ProyectoInterno && t.Presupuesto != null)
            .Include(t => t.Cliente)
            .OrderByDescending(t => t.Presupuesto).Take(15).ToListAsync();
        var margenes = new List<MargenTrabajo>();
        foreach (var t in incluirTrabajos ? activos : [])
        {
            margenes.Add(new MargenTrabajo(t, t.Cliente?.NombreComercial ?? t.Cliente?.Nombre, await _trabajos.RentabilidadAsync(t)));
        }

        var limiteRenov = hoy.AddDays(30);
        return new ResumenFinanzas
        {
            Desde = desde,
            Hasta = hasta,
            Ingresos = confirmados.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.SubtotalMxn),
            Egresos = confirmados.Where(m => m.Tipo == TipoMovimiento.Egreso).Sum(m => m.SubtotalMxn),
            Horas = horas.Sum(h => h.Horas),
            CostoHoras = horas.Sum(h => CostoHora(h.Horas, h.CostoHora, h.MonedaCosto)),
            PorCobrar = pendientes.Where(p => p.Tipo == TipoMovimiento.Ingreso).Sum(p => p.TotalMxn),
            PorCobrarVencido = pendientes.Where(p => p.Tipo == TipoMovimiento.Ingreso && p.FechaProgramada < hoy).Sum(p => p.TotalMxn),
            PorPagar = pendientes.Where(p => p.Tipo == TipoMovimiento.Egreso).Sum(p => p.TotalMxn),
            PorPagarVencido = pendientes.Where(p => p.Tipo == TipoMovimiento.Egreso && p.FechaProgramada < hoy).Sum(p => p.TotalMxn),
            MrrServicios = mrr,
            MrrStripe = mrrStripe,
            SuscripcionesStripe = stripe.Count,
            HayStripe = hayStripe,
            Recurrentes = recurrentes.Count,
            CostoFijoMensual = costoFijo,
            TipoCambio = usd,
            FaltaTipoCambio = faltaTc,
            Flujo = flujo,
            Productos = filasProducto,
            Lineas = lineas,
            Trabajos = margenes.OrderBy(m => m.Rentabilidad.Margen ?? 1).ToList(),
            Renovaciones = servicios.Where(s => s.ProximaRenovacion is not null && s.ProximaRenovacion <= limiteRenov)
                .OrderBy(s => s.ProximaRenovacion).ToList(),
            DominiosPorVencer = dominiosActivos.Where(d => d.FechaVencimiento is not null && d.FechaVencimiento <= limiteRenov)
                .OrderBy(d => d.FechaVencimiento).ToList()
        };
    }

    public static string NombreMes(DateOnly mes) =>
        CultureInfo.GetCultureInfo("es-MX").TextInfo.ToTitleCase(mes.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-MX")));
}
