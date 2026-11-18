using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Comercial.Catalogo;

public class ServicioComercialInput
{
    public static readonly string[] Monedas = ["MXN", "USD"];

    [Required(ErrorMessage = "Ingresa el nombre del servicio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public CategoriaComercial Categoria { get; set; } = CategoriaComercial.Desarrollo;
    public ModalidadCobro Modalidad { get; set; } = ModalidadCobro.PrecioFijo;
    public EstadoCatalogo Estado { get; set; } = EstadoCatalogo.Activo;

    [StringLength(1000, ErrorMessage = "La descripción no puede exceder 1000 caracteres.")]
    public string? Descripcion { get; set; }

    [Range(0, 99_999_999, ErrorMessage = "El precio base debe ser un número positivo.")]
    public decimal? PrecioBase { get; set; }

    public string Moneda { get; set; } = "MXN";

    [StringLength(50, ErrorMessage = "La unidad no puede exceder 50 caracteres.")]
    public string? Unidad { get; set; }

    [Range(0, 99_999_999, ErrorMessage = "El costo estimado debe ser un número positivo.")]
    public decimal? CostoEstimado { get; set; }

    [StringLength(1000, ErrorMessage = "Los entregables no pueden exceder 1000 caracteres.")]
    public string? Entregables { get; set; }

    [StringLength(100, ErrorMessage = "El tiempo estimado no puede exceder 100 caracteres.")]
    public string? TiempoEstimado { get; set; }

    public int? ProductoId { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }

    public static ServicioComercialInput Desde(ServicioComercial s) => new()
    {
        Nombre = s.Nombre, Categoria = s.Categoria, Modalidad = s.Modalidad, Estado = s.Estado,
        Descripcion = s.Descripcion, PrecioBase = s.PrecioBase, Moneda = s.Moneda, Unidad = s.Unidad,
        CostoEstimado = s.CostoEstimado, Entregables = s.Entregables, TiempoEstimado = s.TiempoEstimado,
        ProductoId = s.ProductoId, Notas = s.Notas
    };

    public void AplicarA(ServicioComercial s)
    {
        s.Nombre = Nombre.Trim();
        s.Categoria = Categoria;
        s.Modalidad = Modalidad;
        s.Estado = Estado;
        s.Descripcion = Opciones.Limpio(Descripcion);
        s.PrecioBase = PrecioBase;
        s.Moneda = Moneda;
        s.Unidad = Opciones.Limpio(Unidad);
        s.CostoEstimado = CostoEstimado;
        s.Entregables = Opciones.Limpio(Entregables);
        s.TiempoEstimado = Opciones.Limpio(TiempoEstimado);
        s.ProductoId = ProductoId;
        s.Notas = Opciones.Limpio(Notas);
    }

    /// <summary>Unidad sugerida según la modalidad.</summary>
    public static string UnidadSugerida(ModalidadCobro m) => m switch
    {
        ModalidadCobro.PorHora => "hora",
        ModalidadCobro.Mensual => "mes",
        ModalidadCobro.PorEntregable => "entregable",
        _ => "servicio"
    };
}

public class ServicioComercialModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public ServicioComercialInput Input { get; init; } = new();
    public List<Producto> Productos { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public int Cotizaciones { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Filtro { get; init; }
    public string? Q { get; init; }
}
