using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

/// <summary>
/// Base de las entidades de negocio. Sus altas, cambios y bajas se registran
/// solos en la auditoría (ver AuditoriaInterceptor).
/// </summary>
public abstract class EntidadBase
{
    public int Id { get; set; }

    public DateTime CreadoUtc { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime? ModificadoUtc { get; set; }
    public string? ModificadoPor { get; set; }

    /// <summary>Texto con el que aparece el registro en la auditoría.</summary>
    [NotMapped]
    public abstract string DescripcionAuditoria { get; }
}

/// <summary>Relación con un producto (cliente–producto, servicio–producto) que se audita en su registro padre.</summary>
public interface IRelacionProducto
{
    int ProductoId { get; }
    /// <summary>Registro padre (cliente o servicio) al que pertenece la relación.</summary>
    EntidadBase? Padre { get; }
    /// <summary>Estado de la relación, si aplica (se muestra en la auditoría).</summary>
    string? EstadoRelacion { get; }
}
