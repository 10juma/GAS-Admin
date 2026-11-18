using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Finanzas;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Infraestructura;

public enum TipoVencimiento { Dominio, Ssl, Rotacion, Credencial, Servicio }

/// <summary>Algo que vence o hay que renovar: dominio, certificado, credencial o servicio.</summary>
public sealed record Vencimiento(TipoVencimiento Tipo, string Nombre, DateOnly Fecha, string? Producto, string Pagina, int Id)
{
    public int Dias(DateOnly hoy) => Fecha.DayNumber - hoy.DayNumber;
}

public class ServicioInfraestructura
{
    private readonly GasDbContext _db;

    public ServicioInfraestructura(GasDbContext db) => _db = db;

    public static DateOnly Hoy => ServicioFinanzas.Hoy;

    public async Task<string> SiguienteFolioIncidenteAsync(int anio)
    {
        var prefijo = $"INC-{anio}-";
        var folios = await _db.Incidentes.Where(i => i.Folio.StartsWith(prefijo)).Select(i => i.Folio).ToListAsync();
        var max = folios.Select(f => int.TryParse(f[prefijo.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefijo}{max + 1:0000}";
    }

    /// <summary>
    /// Ajusta el estado de un ambiente según sus incidentes abiertos: crítico → Caído,
    /// alto o medio → Degradado; sin incidentes vuelve a Operativo. No toca Mantenimiento ni Inactivo.
    /// Devuelve el nuevo estado si cambió.
    /// </summary>
    public async Task<EstadoAmbiente?> SincronizarEstadoAsync(int? ambienteId, Incidente? pendiente = null)
    {
        if (ambienteId is null) return null;
        var a = await _db.Ambientes.FirstOrDefaultAsync(x => x.Id == ambienteId);
        if (a is null || a.Estado is EstadoAmbiente.Mantenimiento or EstadoAmbiente.Inactivo) return null;

        var abiertos = await _db.Incidentes
            .Where(i => i.AmbienteId == ambienteId && i.Estado != EstadoIncidente.Resuelto && (pendiente == null || i.Id != pendiente.Id))
            .Select(i => i.Severidad).ToListAsync();
        if (pendiente is not null && pendiente.AmbienteId == ambienteId && pendiente.Abierto) abiertos.Add(pendiente.Severidad);

        var nuevo = abiertos.Count == 0 ? EstadoAmbiente.Operativo
            : abiertos.Contains(SeveridadIncidente.Critica) ? EstadoAmbiente.Caido
            : abiertos.Any(s => s is SeveridadIncidente.Alta or SeveridadIncidente.Media) ? EstadoAmbiente.Degradado
            : EstadoAmbiente.Operativo;
        if (nuevo == a.Estado) return null;
        a.Estado = nuevo;
        return nuevo;
    }

    /// <summary>Actualiza versión y fecha del ambiente con su último despliegue no revertido.</summary>
    public async Task ActualizarVersionAsync(int ambienteId, Despliegue? pendiente = null)
    {
        var a = await _db.Ambientes.FirstOrDefaultAsync(x => x.Id == ambienteId);
        if (a is null) return;
        var lista = await _db.Despliegues.Where(d => d.AmbienteId == ambienteId && d.Resultado != ResultadoDespliegue.Revertido
                                                     && (pendiente == null || d.Id != pendiente.Id))
            .Select(d => new { d.Version, d.FechaUtc }).ToListAsync();
        if (pendiente is not null && pendiente.Resultado != ResultadoDespliegue.Revertido)
            lista.Add(new { pendiente.Version, pendiente.FechaUtc });
        var ultimo = lista.OrderByDescending(d => d.FechaUtc).FirstOrDefault();
        a.Version = ultimo?.Version;
        a.UltimoDespliegueUtc = ultimo?.FechaUtc;
    }

    /// <summary>Dominios, certificados, credenciales y servicios que vencen dentro del horizonte (incluye vencidos).</summary>
    public async Task<List<Vencimiento>> VencimientosAsync(int dias = 60)
    {
        var limite = Hoy.AddDays(dias);
        var lista = new List<Vencimiento>();

        var dominios = await _db.Dominios.AsNoTracking().Where(d => d.Estado == EstadoDominio.Activo)
            .Select(d => new { d.Id, d.Nombre, d.FechaVencimiento, d.SslVence, Producto = d.Producto == null ? null : d.Producto.Nombre })
            .ToListAsync();
        foreach (var d in dominios)
        {
            if (d.FechaVencimiento <= limite) lista.Add(new(TipoVencimiento.Dominio, d.Nombre, d.FechaVencimiento!.Value, d.Producto, "/Infraestructura/Dominios/Index", d.Id));
            if (d.SslVence <= limite) lista.Add(new(TipoVencimiento.Ssl, d.Nombre, d.SslVence!.Value, d.Producto, "/Infraestructura/Dominios/Index", d.Id));
        }

        var credenciales = await _db.Credenciales.AsNoTracking().Where(c => c.Estado == EstadoCredencial.Activa)
            .Include(c => c.Producto).ToListAsync();
        foreach (var c in credenciales)
        {
            if (c.ProximaRotacion <= limite) lista.Add(new(TipoVencimiento.Rotacion, c.Nombre, c.ProximaRotacion!.Value, c.Producto?.Nombre, "/Infraestructura/Credenciales/Index", c.Id));
            if (c.Vence <= limite) lista.Add(new(TipoVencimiento.Credencial, c.Nombre, c.Vence!.Value, c.Producto?.Nombre, "/Infraestructura/Credenciales/Index", c.Id));
        }

        var servicios = await _db.ServiciosExternos.AsNoTracking()
            .Where(s => s.Estado != EstadoServicio.Cancelado && s.ProximaRenovacion != null && s.ProximaRenovacion <= limite)
            .Select(s => new { s.Id, s.Nombre, s.ProximaRenovacion }).ToListAsync();
        lista.AddRange(servicios.Select(s => new Vencimiento(TipoVencimiento.Servicio, s.Nombre, s.ProximaRenovacion!.Value, null, "/ServiciosExternos/Index", s.Id)));

        return lista.OrderBy(v => v.Fecha).ToList();
    }

    public static string Texto(TipoVencimiento t) => t switch
    {
        TipoVencimiento.Dominio => "Dominio",
        TipoVencimiento.Ssl => "Certificado SSL",
        TipoVencimiento.Rotacion => "Rotar credencial",
        TipoVencimiento.Credencial => "Credencial vence",
        _ => "Renovación de servicio"
    };

    public static string Icono(TipoVencimiento t) => t switch
    {
        TipoVencimiento.Dominio => "globe",
        TipoVencimiento.Ssl => "lock",
        TipoVencimiento.Rotacion or TipoVencimiento.Credencial => "key",
        _ => "refresh"
    };
}
