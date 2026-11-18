using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Productos;

public class ProductoInput
{
    [Required(ErrorMessage = "Ingresa el nombre del producto.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres.")]
    public string? Descripcion { get; set; }

    public EstadoProducto Estado { get; set; } = EstadoProducto.EnDesarrollo;
    public ModalidadProducto Modalidad { get; set; } = ModalidadProducto.SaaS;
    public bool TieneWeb { get; set; } = true;
    public bool TieneApp { get; set; } = true;
    public bool UsaStripe { get; set; } = true;

    [Url(ErrorMessage = "El sitio web debe ser una URL completa (https://…).")]
    [StringLength(300)]
    public string? SitioWeb { get; set; }

    public EstadoSuperAdmin SuperAdmin { get; set; } = EstadoSuperAdmin.PorRevisar;

    [Url(ErrorMessage = "La URL del super-admin debe ser completa (https://…).")]
    [StringLength(300)]
    public string? SuperAdminUrl { get; set; }

    public string? ResponsableId { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }

    public static ProductoInput Desde(Producto p) => new()
    {
        Nombre = p.Nombre, Descripcion = p.Descripcion, Estado = p.Estado, Modalidad = p.Modalidad,
        TieneWeb = p.TieneWeb, TieneApp = p.TieneApp, UsaStripe = p.UsaStripe, SitioWeb = p.SitioWeb,
        SuperAdmin = p.SuperAdmin, SuperAdminUrl = p.SuperAdminUrl, ResponsableId = p.ResponsableId, Notas = p.Notas
    };

    public void AplicarA(Producto p)
    {
        p.Nombre = Nombre.Trim();
        p.Descripcion = Opciones.Limpio(Descripcion);
        p.Estado = Estado;
        p.Modalidad = Modalidad;
        p.TieneWeb = TieneWeb;
        p.TieneApp = TieneApp;
        p.UsaStripe = UsaStripe;
        p.SitioWeb = Opciones.Limpio(SitioWeb);
        p.SuperAdmin = SuperAdmin;
        p.SuperAdminUrl = Opciones.Limpio(SuperAdminUrl);
        p.ResponsableId = Opciones.Limpio(ResponsableId);
        p.Notas = Opciones.Limpio(Notas);
    }
}

public class ProductoModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public ProductoInput Input { get; init; } = new();
    public List<OpcionUsuario> Responsables { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public int Clientes { get; init; }
    public int Servicios { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Filtro { get; init; }
}
