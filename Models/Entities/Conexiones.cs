using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

// Fase 6: conexión con el super-admin de cada producto mediante la "API GAS v1" (docs/api-gas-v1.md).
// El secreto compartido vive en configuración (Productos:Conexiones:{clave}:Secreto), nunca aquí.

[DisplayName("Conexión de producto")]
public class ConexionProducto : EntidadBase
{
    [Display(Name = "Producto")] public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    /// <summary>Clave de configuración: el secreto se lee de "Productos:Conexiones:{Clave}:Secreto".</summary>
    [Display(Name = "Clave de configuración")] public string Clave { get; set; } = string.Empty;
    [Display(Name = "URL de la API")] public string UrlApi { get; set; } = string.Empty;
    [Display(Name = "URL del super-admin")] public string? UrlSuperAdmin { get; set; }
    [Display(Name = "Activa")] public bool Activa { get; set; } = true;

    // Último estado reportado (se actualiza solo; excluido de la auditoría).
    public DateTime? UltimaSincronizacionUtc { get; set; }
    public string? UltimoError { get; set; }
    public bool? EstadoOk { get; set; }
    public string? Version { get; set; }
    public string? Ambiente { get; set; }
    public int? LatenciaMs { get; set; }
    /// <summary>Dependencias reportadas por /estado, en JSON.</summary>
    public string? DependenciasJson { get; set; }
    public DateTime? CuentasSincronizadasUtc { get; set; }

    public override string DescripcionAuditoria => $"Conexión · {Clave}";
}

/// <summary>Foto diaria de las métricas de uso de un producto.</summary>
public class MetricaProducto
{
    public int Id { get; set; }
    public int ConexionProductoId { get; set; }
    public ConexionProducto Conexion { get; set; } = null!;
    public DateOnly Fecha { get; set; }
    public int CuentasTotales { get; set; }
    public int CuentasActivas { get; set; }
    public int CuentasEnPrueba { get; set; }
    public int CuentasSuspendidas { get; set; }
    public int UsuariosTotales { get; set; }
    public int UsuariosActivos24h { get; set; }
    public int UsuariosActivos30d { get; set; }
    public int Altas30d { get; set; }
    public int Bajas30d { get; set; }
    /// <summary>Métricas propias del producto, en JSON (clave, nombre, valor, unidad).</summary>
    public string? AdicionalesJson { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Cuenta (tenant) de un producto: negocio, organización, vecindario…</summary>
public class CuentaProducto
{
    public int Id { get; set; }
    public int ConexionProductoId { get; set; }
    public ConexionProducto Conexion { get; set; } = null!;
    public string ExternoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Plan { get; set; }
    /// <summary>activa, prueba, suspendida o cancelada.</summary>
    public string Estado { get; set; } = "activa";
    public DateTime? CreadaUtc { get; set; }
    public DateTime? UltimoUsoUtc { get; set; }
    public DateTime? PruebaHastaUtc { get; set; }
    public int Usuarios { get; set; }
    public string? StripeCustomerId { get; set; }
    /// <summary>Liga opcional a un cliente de GAS.</summary>
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;

    [NotMapped] public bool Vigente => Estado is "activa" or "prueba";
}
