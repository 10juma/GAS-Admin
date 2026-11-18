namespace GAS.Admin.Models.Entities;

/// <summary>Registro de la bitácora de auditoría.</summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Clave de la acción (ver <see cref="GAS.Admin.Services.Auditoria.AccionesAuditoria"/>).</summary>
    public string Accion { get; set; } = string.Empty;

    public string? UsuarioId { get; set; }

    /// <summary>Correo de quien hizo la acción (o el correo intentado en un login fallido).</summary>
    public string? UsuarioEmail { get; set; }

    public string? Entidad { get; set; }

    public string? EntidadId { get; set; }

    public string? Detalle { get; set; }

    public string? Ip { get; set; }

    public string? UserAgent { get; set; }
}
