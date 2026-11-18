using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

// Infraestructura y tecnología: ambientes, repositorios, despliegues, dominios, incidentes y credenciales.
// Captura manual; las integraciones (GitHub, Sentry, UptimeRobot) llegarán en la Fase 5.

public enum TipoAmbiente { Produccion, Staging, Pruebas, Desarrollo }

public enum PlataformaAmbiente { Web, Api, AppMovil, Backoffice, Otro }

public enum EstadoAmbiente { Operativo, Degradado, Caido, Mantenimiento, Inactivo }

public enum VisibilidadRepositorio { Privado, Publico }

public enum ResultadoDespliegue { Exitoso, ConProblemas, Revertido }

public enum EstadoDominio { Activo, Cancelado, Transferido }

public enum SeveridadIncidente { Baja, Media, Alta, Critica }

public enum EstadoIncidente { Abierto, Mitigado, Resuelto }

public enum TipoCredencial { ApiKey, Token, Contrasena, ClaveSsh, Certificado, Webhook, Otro }

public enum EstadoCredencial { Activa, Revocada }

[DisplayName("Repositorio")]
public class Repositorio : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "URL")] public string? Url { get; set; }
    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Proveedor")] public int? ProveedorId { get; set; }
    public ServicioExterno? Proveedor { get; set; }
    [Display(Name = "Rama principal")] public string RamaPrincipal { get; set; } = "main";
    [Display(Name = "Visibilidad")] public VisibilidadRepositorio Visibilidad { get; set; } = VisibilidadRepositorio.Privado;
    [Display(Name = "Tecnología")] public string? Tecnologia { get; set; }
    [Display(Name = "Responsable")] public string? ResponsableId { get; set; }
    public ApplicationUser? Responsable { get; set; }
    [Display(Name = "Archivado")] public bool Archivado { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public override string DescripcionAuditoria => Nombre;
}

[DisplayName("Ambiente")]
public class Ambiente : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Producto")] public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    [Display(Name = "Tipo")] public TipoAmbiente Tipo { get; set; } = TipoAmbiente.Produccion;
    [Display(Name = "Plataforma")] public PlataformaAmbiente Plataforma { get; set; } = PlataformaAmbiente.Web;
    [Display(Name = "URL")] public string? Url { get; set; }
    [Display(Name = "Hosting")] public int? HostingId { get; set; }
    public ServicioExterno? Hosting { get; set; }
    [Display(Name = "Repositorio")] public int? RepositorioId { get; set; }
    public Repositorio? Repositorio { get; set; }
    [Display(Name = "Rama")] public string? Rama { get; set; }
    [Display(Name = "Estado")] public EstadoAmbiente Estado { get; set; } = EstadoAmbiente.Operativo;
    /// <summary>Versión del último despliegue exitoso (se actualiza con la bitácora).</summary>
    [Display(Name = "Versión")] public string? Version { get; set; }
    [Display(Name = "Último despliegue")] public DateTime? UltimoDespliegueUtc { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public List<Despliegue> Despliegues { get; set; } = [];

    public override string DescripcionAuditoria => Nombre;
}

[DisplayName("Despliegue")]
public class Despliegue : EntidadBase
{
    public int AmbienteId { get; set; }
    public Ambiente Ambiente { get; set; } = null!;
    [Display(Name = "Versión")] public string Version { get; set; } = string.Empty;
    [Display(Name = "Fecha")] public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    [Display(Name = "Resultado")] public ResultadoDespliegue Resultado { get; set; } = ResultadoDespliegue.Exitoso;
    [Display(Name = "Rama o commit")] public string? Referencia { get; set; }
    [Display(Name = "Realizó")] public string? Realizo { get; set; }
    /// <summary>Origen externo (por ejemplo "github-release:123") para no registrar dos veces lo mismo.</summary>
    [Display(Name = "Origen externo")] public string? ExternoId { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public override string DescripcionAuditoria => $"{Version} · {FechaUtc:dd/MM/yyyy}";
}

[DisplayName("Dominio")]
public class Dominio : EntidadBase
{
    [Display(Name = "Dominio")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Registrador")] public int? RegistradorId { get; set; }
    public ServicioExterno? Registrador { get; set; }
    [Display(Name = "DNS")] public string? Dns { get; set; }
    [Display(Name = "Vencimiento")] public DateOnly? FechaVencimiento { get; set; }
    /// <summary>Años que cubre cada renovación.</summary>
    [Display(Name = "Años por renovación")] public int AniosRenovacion { get; set; } = 1;
    [Display(Name = "Renovación automática")] public bool RenovacionAutomatica { get; set; }
    [Display(Name = "Costo de renovación")] public decimal? Costo { get; set; }
    [Display(Name = "Moneda")] public string Moneda { get; set; } = "USD";
    [Display(Name = "SSL vence")] public DateOnly? SslVence { get; set; }
    [Display(Name = "Emisor SSL")] public string? SslEmisor { get; set; }
    [Display(Name = "Estado")] public EstadoDominio Estado { get; set; } = EstadoDominio.Activo;
    [Display(Name = "Notas")] public string? Notas { get; set; }

    [NotMapped] public decimal? CostoMensual => Costo is null || Estado != EstadoDominio.Activo ? null : Math.Round(Costo.Value / (12m * Math.Max(1, AniosRenovacion)), 2);

    public override string DescripcionAuditoria => Nombre;
}

[DisplayName("Incidente")]
public class Incidente : EntidadBase
{
    [Display(Name = "Folio")] public string Folio { get; set; } = string.Empty;
    [Display(Name = "Título")] public string Titulo { get; set; } = string.Empty;
    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Ambiente")] public int? AmbienteId { get; set; }
    public Ambiente? Ambiente { get; set; }
    [Display(Name = "Severidad")] public SeveridadIncidente Severidad { get; set; } = SeveridadIncidente.Media;
    [Display(Name = "Estado")] public EstadoIncidente Estado { get; set; } = EstadoIncidente.Abierto;
    [Display(Name = "Inicio")] public DateTime InicioUtc { get; set; } = DateTime.UtcNow;
    [Display(Name = "Resuelto")] public DateTime? FinUtc { get; set; }
    [Display(Name = "Impacto")] public string? Impacto { get; set; }
    [Display(Name = "Causa")] public string? Causa { get; set; }
    [Display(Name = "Acciones")] public string? Acciones { get; set; }
    [Display(Name = "Responsable")] public string? ResponsableId { get; set; }
    public ApplicationUser? Responsable { get; set; }

    [NotMapped] public bool Abierto => Estado != EstadoIncidente.Resuelto;
    [NotMapped] public TimeSpan Duracion => (FinUtc ?? DateTime.UtcNow) - InicioUtc;

    public override string DescripcionAuditoria => $"{Folio} · {Titulo}";
}

[DisplayName("Credencial")]
public class Credencial : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Tipo")] public TipoCredencial Tipo { get; set; } = TipoCredencial.ApiKey;
    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Ambiente")] public int? AmbienteId { get; set; }
    public Ambiente? Ambiente { get; set; }
    [Display(Name = "Servicio externo")] public int? ServicioExternoId { get; set; }
    public ServicioExterno? ServicioExterno { get; set; }
    /// <summary>Dónde está guardada (gestor de secretos). Nunca el valor.</summary>
    [Display(Name = "Referencia")] public string Referencia { get; set; } = string.Empty;
    [Display(Name = "Responsable")] public string? ResponsableId { get; set; }
    public ApplicationUser? Responsable { get; set; }
    [Display(Name = "Última rotación")] public DateOnly? UltimaRotacion { get; set; }
    [Display(Name = "Rotar cada (días)")] public int? RotacionDias { get; set; }
    [Display(Name = "Vence")] public DateOnly? Vence { get; set; }
    [Display(Name = "Estado")] public EstadoCredencial Estado { get; set; } = EstadoCredencial.Activa;
    [Display(Name = "Notas")] public string? Notas { get; set; }

    [NotMapped] public DateOnly? ProximaRotacion => UltimaRotacion is null || RotacionDias is null or <= 0 ? null : UltimaRotacion.Value.AddDays(RotacionDias.Value);

    /// <summary>La fecha más próxima entre rotación y vencimiento.</summary>
    [NotMapped] public DateOnly? ProximaAccion => (ProximaRotacion, Vence) switch
    {
        (null, null) => null,
        ({ } r, null) => r,
        (null, { } v) => v,
        ({ } r, { } v) => r < v ? r : v
    };

    public override string DescripcionAuditoria => Nombre;
}
