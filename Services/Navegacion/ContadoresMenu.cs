using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GAS.Admin.Services.Navegacion;

public sealed record Contadores(int Usuarios, int Roles, int Alertas24h, int Productos, int ClientesActivos, int Renovaciones30d,
    int OportunidadesAbiertas, int CotizacionesPorVencer, int PersonasActivas, int TrabajosActivos, int TareasVencidas, int MovimientosVencidos,
    int IncidentesAbiertos, int DominiosPorVencer, int CredencialesPorRotar, int PagosFallidosStripe,
    int ErroresNuevos, int IntegracionesConError, int ConexionesConError,
    int TicketsAbiertos, int TicketsVencidos);

/// <summary>
/// Contadores del menú lateral. Se calculan con UNA sola consulta (la base es remota) y se guardan
/// en caché 60 s; si varias páginas los piden a la vez, solo una los recalcula.
/// </summary>
public class ContadoresMenu
{
    private const string Clave = "GAS.ContadoresMenu";
    private static readonly SemaphoreSlim Candado = new(1, 1);

    private readonly GasDbContext _db;
    private readonly IMemoryCache _cache;

    public ContadoresMenu(GasDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private sealed class Fila
    {
        public int Usuarios { get; set; }
        public int Roles { get; set; }
        public int Alertas24h { get; set; }
        public int Productos { get; set; }
        public int ClientesActivos { get; set; }
        public int Renovaciones30d { get; set; }
        public int OportunidadesAbiertas { get; set; }
        public int CotizacionesPorVencer { get; set; }
        public int PersonasActivas { get; set; }
        public int TrabajosActivos { get; set; }
        public int TareasVencidas { get; set; }
        public int MovimientosVencidos { get; set; }
        public int IncidentesAbiertos { get; set; }
        public int DominiosPorVencer { get; set; }
        public int CredencialesPorRotar { get; set; }
        public int PagosFallidosStripe { get; set; }
        public int ErroresNuevos { get; set; }
        public int IntegracionesConError { get; set; }
        public int ConexionesConError { get; set; }
        public int TicketsAbiertos { get; set; }
        public int TicketsVencidos { get; set; }
    }

    public async Task<Contadores> ObtenerAsync()
    {
        if (_cache.TryGetValue(Clave, out Contadores? enCache) && enCache is not null) return enCache;
        await Candado.WaitAsync();
        try
        {
            if (_cache.TryGetValue(Clave, out enCache) && enCache is not null) return enCache;
            var c = await CalcularAsync();
            _cache.Set(Clave, c, TimeSpan.FromSeconds(60));
            return c;
        }
        finally
        {
            Candado.Release();
        }
    }

    private async Task<Contadores> CalcularAsync()
    {
        var ahora = DateTime.UtcNow;
        var hoy = DateOnly.FromDateTime(Fechas.ALocal(ahora));
        // Acciones fijas del código (no datos de usuario): se pueden poner en el SQL.
        var alertas = string.Join(",", AccionesAuditoria.DeCategoria(CategoriaAccion.Alertas).Select(a => $"N'{a.Replace("'", "''")}'"));
        var sql = $"""
            SELECT
              (SELECT COUNT(*) FROM AspNetUsers) AS Usuarios,
              (SELECT COUNT(*) FROM AspNetRoles) AS Roles,
              (SELECT COUNT(*) FROM AuditLogs WHERE FechaUtc >= @desde AND Accion IN ({alertas})) AS Alertas24h,
              (SELECT COUNT(*) FROM Productos) AS Productos,
              (SELECT COUNT(*) FROM Clientes WHERE Estado = N'Activo') AS ClientesActivos,
              (SELECT COUNT(*) FROM ServiciosExternos WHERE Estado <> N'Cancelado' AND ProximaRenovacion IS NOT NULL AND ProximaRenovacion <= @limite) AS Renovaciones30d,
              (SELECT COUNT(*) FROM Oportunidades WHERE Etapa NOT IN (N'Ganada', N'Perdida')) AS OportunidadesAbiertas,
              (SELECT COUNT(*) FROM Cotizaciones WHERE Estado = N'Enviada' AND DATEADD(day, VigenciaDias, FechaEmision) <= DATEADD(day, 5, @hoy)) AS CotizacionesPorVencer,
              (SELECT COUNT(*) FROM Personas WHERE Activa = 1) AS PersonasActivas,
              (SELECT COUNT(*) FROM Trabajos WHERE Estado NOT IN (N'Cerrado', N'Cancelado')) AS TrabajosActivos,
              (SELECT COUNT(*) FROM Tareas WHERE Estado <> N'Terminado' AND FechaVencimiento < @hoy) AS TareasVencidas,
              (SELECT COUNT(*) FROM Movimientos WHERE Estado = N'Esperado' AND FechaProgramada < @hoy) AS MovimientosVencidos,
              (SELECT COUNT(*) FROM Incidentes WHERE Estado <> N'Resuelto') AS IncidentesAbiertos,
              (SELECT COUNT(*) FROM Dominios WHERE Estado = N'Activo' AND (FechaVencimiento <= @limite OR SslVence <= @limite)) AS DominiosPorVencer,
              (SELECT COUNT(*) FROM Credenciales WHERE Estado = N'Activa' AND (Vence <= @limite
                  OR (UltimaRotacion IS NOT NULL AND RotacionDias > 0 AND DATEADD(day, RotacionDias, UltimaRotacion) <= @limite))) AS CredencialesPorRotar,
              (SELECT COUNT(*) FROM FacturasStripe WHERE (Estado = N'open' AND Intentos > 0) OR Estado = N'uncollectible') AS PagosFallidosStripe,
              (SELECT COALESCE(SUM(Nuevos24h), 0) FROM ProyectosSentry WHERE Existe = 1) AS ErroresNuevos,
              (SELECT COUNT(*) FROM EstadosIntegracion WHERE UltimoError IS NOT NULL) AS IntegracionesConError,
              (SELECT COUNT(*) FROM ConexionesProducto WHERE Activa = 1 AND (UltimoError IS NOT NULL OR EstadoOk = 0)) AS ConexionesConError,
              (SELECT COUNT(*) FROM Tickets WHERE Estado NOT IN (N'Resuelto', N'Cerrado')) AS TicketsAbiertos,
              (SELECT COUNT(*) FROM Tickets WHERE Estado NOT IN (N'Resuelto', N'Cerrado')
                  AND ((PrimeraRespuestaUtc IS NULL AND VencePrimeraRespuestaUtc < @ahora) OR VenceResolucionUtc < @ahora)) AS TicketsVencidos
            """;
        var f = (await _db.Database.SqlQueryRaw<Fila>(sql,
                new Microsoft.Data.SqlClient.SqlParameter("@desde", ahora.AddHours(-24)),
                new Microsoft.Data.SqlClient.SqlParameter("@ahora", ahora),
                new Microsoft.Data.SqlClient.SqlParameter("@hoy", System.Data.SqlDbType.Date) { Value = hoy.ToDateTime(TimeOnly.MinValue) },
                new Microsoft.Data.SqlClient.SqlParameter("@limite", System.Data.SqlDbType.Date) { Value = hoy.AddDays(30).ToDateTime(TimeOnly.MinValue) })
            .ToListAsync()).First();
        return new Contadores(f.Usuarios, f.Roles, f.Alertas24h, f.Productos, f.ClientesActivos, f.Renovaciones30d,
            f.OportunidadesAbiertas, f.CotizacionesPorVencer, f.PersonasActivas, f.TrabajosActivos, f.TareasVencidas, f.MovimientosVencidos,
            f.IncidentesAbiertos, f.DominiosPorVencer, f.CredencialesPorRotar, f.PagosFallidosStripe,
            f.ErroresNuevos, f.IntegracionesConError, f.ConexionesConError, f.TicketsAbiertos, f.TicketsVencidos);
    }

    /// <summary>Llamar tras crear o eliminar registros para ver el cambio de inmediato.</summary>
    public void Invalidar() => _cache.Remove(Clave);
}
