using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

// ===================== Productos =====================

public enum EstadoProducto { EnOperacion, EnDesarrollo, Pausado, Retirado }

public enum ModalidadProducto { SaaS, Renta, Licencia, PorDefinir }

public enum EstadoSuperAdmin { Existe, PorRevisar, NoTiene }

[DisplayName("Producto")]
public class Producto : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Descripción")] public string? Descripcion { get; set; }
    [Display(Name = "Estado")] public EstadoProducto Estado { get; set; } = EstadoProducto.EnDesarrollo;
    [Display(Name = "Modalidad")] public ModalidadProducto Modalidad { get; set; } = ModalidadProducto.SaaS;
    [Display(Name = "Web")] public bool TieneWeb { get; set; } = true;
    [Display(Name = "App móvil")] public bool TieneApp { get; set; } = true;
    [Display(Name = "Usa Stripe")] public bool UsaStripe { get; set; } = true;
    [Display(Name = "Sitio web")] public string? SitioWeb { get; set; }
    [Display(Name = "Super-admin")] public EstadoSuperAdmin SuperAdmin { get; set; } = EstadoSuperAdmin.PorRevisar;
    [Display(Name = "URL del super-admin")] public string? SuperAdminUrl { get; set; }
    [Display(Name = "Responsable técnico")] public string? ResponsableId { get; set; }
    public ApplicationUser? Responsable { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public List<ClienteProducto> Clientes { get; set; } = [];
    public List<ServicioProducto> Servicios { get; set; } = [];

    public override string DescripcionAuditoria => Nombre;
}

// ===================== Clientes =====================

public enum EstadoCliente { Prospecto, Activo, Inactivo }

public enum EstadoClienteProducto { Prueba, Activo, Cancelado }

[DisplayName("Cliente")]
public class Cliente : EntidadBase
{
    [Display(Name = "Nombre o razón social")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Nombre comercial")] public string? NombreComercial { get; set; }
    [Display(Name = "RFC")] public string? Rfc { get; set; }
    [Display(Name = "Estado")] public EstadoCliente Estado { get; set; } = EstadoCliente.Prospecto;
    [Display(Name = "Ciudad")] public string? Ciudad { get; set; }
    [Display(Name = "Sitio web")] public string? SitioWeb { get; set; }
    [Display(Name = "Contacto")] public string? ContactoNombre { get; set; }
    [Display(Name = "Correo del contacto")] public string? ContactoEmail { get; set; }
    [Display(Name = "Teléfono del contacto")] public string? ContactoTelefono { get; set; }
    [Display(Name = "Responsable")] public string? ResponsableId { get; set; }
    public ApplicationUser? Responsable { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public List<ClienteProducto> Productos { get; set; } = [];

    [NotMapped] public string NombreVisible => string.IsNullOrWhiteSpace(NombreComercial) ? Nombre : NombreComercial;

    public override string DescripcionAuditoria => NombreVisible;
}

/// <summary>Un cliente puede usar varios productos sin duplicar su información.</summary>
public class ClienteProducto : IRelacionProducto
{
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public EstadoClienteProducto Estado { get; set; } = EstadoClienteProducto.Activo;
    public DateOnly? Desde { get; set; }

    EntidadBase? IRelacionProducto.Padre => Cliente;
    string? IRelacionProducto.EstadoRelacion => Estado.ToString();
}

// ===================== Servicios externos =====================

public enum CategoriaServicio { Pagos, Dominios, Hosting, Desarrollo, Monitoreo, Disponibilidad, ServiciosMoviles, Correo, Despliegue, Otro }

public enum PeriodicidadCobro { Mensual, Trimestral, Anual, PorUso, Unico, Gratuito }

public enum EstadoServicio { Activo, EnPrueba, Cancelado }

[DisplayName("Servicio externo")]
public class ServicioExterno : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Categoría")] public CategoriaServicio Categoria { get; set; } = CategoriaServicio.Otro;
    [Display(Name = "Uso")] public string? Descripcion { get; set; }
    [Display(Name = "Estado")] public EstadoServicio Estado { get; set; } = EstadoServicio.Activo;
    [Display(Name = "Panel administrativo")] public string? UrlPanel { get; set; }
    [Display(Name = "Cuenta asociada")] public string? CuentaAsociada { get; set; }
    [Display(Name = "Responsable")] public string? ResponsableId { get; set; }
    public ApplicationUser? Responsable { get; set; }
    [Display(Name = "Plan")] public string? Plan { get; set; }
    [Display(Name = "Costo")] public decimal? Costo { get; set; }
    [Display(Name = "Moneda")] public string Moneda { get; set; } = "MXN";
    [Display(Name = "Periodicidad")] public PeriodicidadCobro Periodicidad { get; set; } = PeriodicidadCobro.Mensual;
    [Display(Name = "Próxima renovación")] public DateOnly? ProximaRenovacion { get; set; }
    /// <summary>Día del mes en que renueva (para no recorrerlo: 31 → 28 → 28…).</summary>
    [Display(Name = "Día de renovación")] public int? DiaRenovacion { get; set; }
    [Display(Name = "Referencia de credenciales")] public string? ReferenciaCredenciales { get; set; }
    [Display(Name = "Documentación")] public string? DocumentacionUrl { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public List<ServicioProducto> Productos { get; set; } = [];

    /// <summary>Costo equivalente por mes en su moneda (null si es variable, único o gratuito).</summary>
    [NotMapped]
    public decimal? CostoMensual => Costo is null || Estado == EstadoServicio.Cancelado ? null : Periodicidad switch
    {
        PeriodicidadCobro.Mensual => Costo,
        PeriodicidadCobro.Trimestral => Math.Round(Costo.Value / 3, 2),
        PeriodicidadCobro.Anual => Math.Round(Costo.Value / 12, 2),
        _ => null
    };

    public override string DescripcionAuditoria => Nombre;
}

/// <summary>Productos que dependen de un servicio (base para repartir su costo en partes iguales).</summary>
public class ServicioProducto : IRelacionProducto
{
    public int ServicioExternoId { get; set; }
    public ServicioExterno ServicioExterno { get; set; } = null!;
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    EntidadBase? IRelacionProducto.Padre => ServicioExterno;
    string? IRelacionProducto.EstadoRelacion => null;
}
