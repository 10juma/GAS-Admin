using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

// Fase 5: integraciones técnicas de solo lectura (UptimeRobot, GitHub, Sentry).
// Son copias locales que se actualizan solas; no heredan de EntidadBase para no llenar la auditoría.
// Lo que sí se audita es lo que generan en los registros propios: incidentes y despliegues.

/// <summary>Estado de la última sincronización de cada integración.</summary>
public class EstadoIntegracion
{
    /// <summary>UptimeRobot, GitHub o Sentry.</summary>
    public string Nombre { get; set; } = string.Empty;
    public DateTime? UltimaSincronizacionUtc { get; set; }
    public string? UltimoResultado { get; set; }
    public string? UltimoError { get; set; }
}

public enum EstadoMonitor { Desconocido, Arriba, Dudoso, Caido, Pausado }

public class MonitorUptime
{
    public int Id { get; set; }
    public string ExternoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Url { get; set; }
    public EstadoMonitor Estado { get; set; } = EstadoMonitor.Desconocido;
    /// <summary>Porcentaje de disponibilidad de los últimos 30 días.</summary>
    public decimal? Uptime30d { get; set; }
    public DateTime? UltimoCambioUtc { get; set; }
    /// <summary>Ambiente que vigila este monitor (se liga solo por URL o a mano).</summary>
    public int? AmbienteId { get; set; }
    public Ambiente? Ambiente { get; set; }
    /// <summary>Incidente abierto automáticamente mientras el monitor está caído.</summary>
    public int? IncidenteId { get; set; }
    public Incidente? Incidente { get; set; }
    public bool Existe { get; set; } = true;
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Lo que GitHub reporta de un repositorio registrado.</summary>
public class RepoGitHub
{
    public int RepositorioId { get; set; }
    public Repositorio Repositorio { get; set; } = null!;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? RamaPrincipal { get; set; }
    public bool Privado { get; set; }
    public string? UltimoCommitSha { get; set; }
    public string? UltimoCommitMensaje { get; set; }
    public string? UltimoCommitAutor { get; set; }
    public DateTime? UltimoCommitUtc { get; set; }
    public int PrsAbiertos { get; set; }
    public int IssuesAbiertos { get; set; }
    public string? UltimoRelease { get; set; }
    public DateTime? UltimoReleaseUtc { get; set; }
    public string? Error { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;

    [NotMapped] public string? ShaCorto => UltimoCommitSha is { Length: >= 7 } s ? s[..7] : UltimoCommitSha;
}

public class ProyectoSentry
{
    public int Id { get; set; }
    public string ExternoId { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Plataforma { get; set; }
    public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int SinResolver { get; set; }
    public int Nuevos24h { get; set; }
    public bool Existe { get; set; } = true;
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;

    public List<ErrorSentry> Errores { get; set; } = [];
}

public class ErrorSentry
{
    public int Id { get; set; }
    public int ProyectoSentryId { get; set; }
    public ProyectoSentry Proyecto { get; set; } = null!;
    public string ExternoId { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Culpable { get; set; }
    public string? Nivel { get; set; }
    public int Eventos { get; set; }
    public int Usuarios { get; set; }
    public DateTime? PrimeraVezUtc { get; set; }
    public DateTime? UltimaVezUtc { get; set; }
    public string? Url { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
}
