using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Finanzas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Proyectos;

/// <summary>Resultado económico de un trabajo, en su moneda.</summary>
public sealed record Rentabilidad(
    string Moneda,
    decimal? Ingreso,
    decimal CostoHoras,
    decimal GastosDirectos,
    decimal HorasTotales,
    decimal HorasSinCosto,
    Dictionary<string, decimal> CostoOtrasMonedas,
    decimal? MargenEstimado,
    decimal Egresos = 0,
    decimal Cobrado = 0,
    decimal PorCobrar = 0)
{
    public decimal CostoTotal => CostoHoras + GastosDirectos + Egresos;
    public decimal? Utilidad => Ingreso is null ? null : Ingreso - CostoTotal;
    public decimal? Margen => Ingreso is > 0 ? Math.Round((Ingreso.Value - CostoTotal) / Ingreso.Value, 4) : null;
}

public class ServicioTrabajos
{
    private readonly GasDbContext _db;
    private readonly ServicioFinanzas _finanzas;

    public ServicioTrabajos(GasDbContext db, ServicioFinanzas finanzas)
    {
        _db = db;
        _finanzas = finanzas;
    }

    public static DateOnly Hoy => DateOnly.FromDateTime(Fechas.ALocal(DateTime.UtcNow));
    public static DateOnly InicioMes(DateOnly d) => new(d.Year, d.Month, 1);

    public async Task<string> SiguienteFolioAsync(int anio)
    {
        var prefijo = $"TRB-{anio}-";
        var folios = await _db.Trabajos.Where(t => t.Folio.StartsWith(prefijo)).Select(t => t.Folio).ToListAsync();
        var max = folios.Select(f => int.TryParse(f[prefijo.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefijo}{max + 1:0000}";
    }

    /// <summary>Persona del equipo ligada a un usuario (o null).</summary>
    public Task<Persona?> PersonaDeUsuarioAsync(string? usuarioId) =>
        usuarioId is null ? Task.FromResult<Persona?>(null) : _db.Personas.FirstOrDefaultAsync(p => p.UsuarioId == usuarioId);

    /// <summary>
    /// Crea el trabajo de una cotización aprobada: copia presupuesto y costo estimado,
    /// convierte cada concepto en entregable, si es recurrente abre el ciclo del mes
    /// y registra en Finanzas los cobros esperados según la condición de pago (sin guardar).
    /// </summary>
    public async Task<(Trabajo Trabajo, int Cobros)> CrearDesdeCotizacionAsync(Cotizacion c)
    {
        var tipo = c.TipoTrabajo switch
        {
            Models.Entities.TipoTrabajo.ServicioRecurrente => TipoTrabajoOperativo.ServicioRecurrente,
            Models.Entities.TipoTrabajo.TrabajoPuntual => TipoTrabajoOperativo.TrabajoPuntual,
            _ => TipoTrabajoOperativo.Proyecto
        };
        var responsable = await PersonaDeUsuarioAsync(c.ResponsableId);
        var lineas = c.Lineas.Count > 0 ? c.Lineas : await _db.CotizacionLineas.Where(l => l.CotizacionId == c.Id).ToListAsync();

        var t = new Trabajo
        {
            Folio = await SiguienteFolioAsync(Hoy.Year),
            Nombre = c.Titulo,
            Tipo = tipo,
            Estado = EstadoTrabajo.PorIniciar,
            ClienteId = c.ClienteId,
            CotizacionId = c.Id,
            ResponsableId = responsable?.Id,
            DepartamentoId = responsable?.DepartamentoId,
            FechaInicio = Hoy,
            Presupuesto = c.Subtotal,
            Moneda = c.Moneda,
            CostoEstimado = c.CostoEstimado,
            Entregables = lineas.OrderBy(l => l.Orden).Select((l, i) => new Entregable
            {
                Nombre = l.Concepto.Length > 200 ? l.Concepto[..200] : l.Concepto,
                Descripcion = l.Descripcion,
                Orden = i
            }).ToList()
        };
        if (tipo == TipoTrabajoOperativo.ServicioRecurrente)
        {
            t.Ciclos.Add(new CicloTrabajo { Periodo = InicioMes(Hoy), Monto = c.Subtotal });
        }
        _db.Trabajos.Add(t);
        var cobros = await _finanzas.GenerarCobrosDeCotizacionAsync(c, t);
        return (t, cobros.Count);
    }

    /// <summary>Agrega (sin guardar) el ciclo mensual de un periodo y su cobro esperado.</summary>
    public async Task<CicloTrabajo> AgregarCicloAsync(Trabajo t, DateOnly periodo)
    {
        var ciclo = new CicloTrabajo { Trabajo = t, Periodo = InicioMes(periodo), Monto = t.Presupuesto };
        t.Ciclos.Add(ciclo);
        await _finanzas.GenerarCobroDeCicloAsync(t, ciclo);
        return ciclo;
    }

    public async Task<Rentabilidad> RentabilidadAsync(Trabajo t)
    {
        var horas = await _db.RegistrosHoras.AsNoTracking().Where(h => h.TrabajoId == t.Id)
            .Select(h => new { h.Horas, h.CostoHora, h.MonedaCosto }).ToListAsync();
        var ingreso = t.Presupuesto;
        if (t.Tipo == TipoTrabajoOperativo.ServicioRecurrente && await _db.Ciclos.AnyAsync(c => c.TrabajoId == t.Id && c.Monto != null))
            ingreso = await _db.Ciclos.Where(c => c.TrabajoId == t.Id).SumAsync(c => c.Monto ?? 0);

        var mismas = horas.Where(h => h.CostoHora is not null && (h.MonedaCosto ?? "MXN") == t.Moneda).ToList();
        var otras = horas.Where(h => h.CostoHora is not null && (h.MonedaCosto ?? "MXN") != t.Moneda)
            .GroupBy(h => h.MonedaCosto ?? "MXN").ToDictionary(g => g.Key, g => g.Sum(h => Math.Round(h.Horas * h.CostoHora!.Value, 2)));

        decimal? margenEstimado = t.Presupuesto is > 0 && t.CostoEstimado is not null
            ? Math.Round((t.Presupuesto.Value - t.CostoEstimado.Value) / t.Presupuesto.Value, 4) : null;

        // Movimientos de Finanzas ligados al trabajo (sin IVA, en la moneda del trabajo).
        var movimientos = await _db.Movimientos.AsNoTracking().Where(m => m.TrabajoId == t.Id && m.Estado != EstadoMovimiento.Cancelado)
            .Select(m => new { m.Tipo, m.Estado, m.Moneda, m.Subtotal, m.SubtotalMxn }).ToListAsync();
        decimal? EnMoneda(string moneda, decimal subtotal, decimal subtotalMxn) =>
            moneda == t.Moneda ? subtotal : t.Moneda == "MXN" ? subtotalMxn : null;
        decimal egresos = 0, cobrado = 0, porCobrar = 0;
        foreach (var m in movimientos)
        {
            var monto = EnMoneda(m.Moneda, m.Subtotal, m.SubtotalMxn);
            if (monto is null)
            {
                if (m.Tipo == TipoMovimiento.Egreso) otras[m.Moneda] = otras.GetValueOrDefault(m.Moneda) + m.Subtotal;
                continue;
            }
            if (m.Tipo == TipoMovimiento.Egreso) egresos += monto.Value;
            else if (m.Estado == EstadoMovimiento.Confirmado) cobrado += monto.Value;
            else porCobrar += monto.Value;
        }

        return new Rentabilidad(
            t.Moneda, ingreso,
            mismas.Sum(h => Math.Round(h.Horas * h.CostoHora!.Value, 2)),
            t.GastosDirectos,
            horas.Sum(h => h.Horas),
            horas.Where(h => h.CostoHora is null).Sum(h => h.Horas),
            otras,
            margenEstimado,
            egresos, cobrado, porCobrar);
    }
}

public static class Navegacion
{
    /// <summary>Regresa a una URL local (de la página que abrió el modal) o a la página indicada.</summary>
    public static IActionResult VolverA(this PageModel page, string? volver, string pagina, object? ruta = null) =>
        !string.IsNullOrEmpty(volver) && page.Url.IsLocalUrl(volver)
            ? page.LocalRedirect(volver)
            : page.RedirectToPage(pagina, ruta);
}
