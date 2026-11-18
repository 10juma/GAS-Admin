using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.ServiciosExternos;

public class ServicioInput
{
    public static readonly string[] Monedas = ["MXN", "USD", "EUR"];

    [Required(ErrorMessage = "Ingresa el nombre del servicio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public CategoriaServicio Categoria { get; set; } = CategoriaServicio.Otro;

    [StringLength(500, ErrorMessage = "El uso no puede exceder 500 caracteres.")]
    public string? Descripcion { get; set; }

    public EstadoServicio Estado { get; set; } = EstadoServicio.Activo;

    [Url(ErrorMessage = "El panel administrativo debe ser una URL completa (https://…).")]
    [StringLength(300)]
    public string? UrlPanel { get; set; }

    [StringLength(200, ErrorMessage = "La cuenta asociada no puede exceder 200 caracteres.")]
    public string? CuentaAsociada { get; set; }

    public string? ResponsableId { get; set; }

    [StringLength(100, ErrorMessage = "El plan no puede exceder 100 caracteres.")]
    public string? Plan { get; set; }

    [Range(0, 99_999_999, ErrorMessage = "El costo debe ser un número positivo.")]
    public decimal? Costo { get; set; }

    public string Moneda { get; set; } = "USD";

    public PeriodicidadCobro Periodicidad { get; set; } = PeriodicidadCobro.Mensual;

    public DateOnly? ProximaRenovacion { get; set; }

    [StringLength(300, ErrorMessage = "La referencia no puede exceder 300 caracteres.")]
    public string? ReferenciaCredenciales { get; set; }

    [Url(ErrorMessage = "La documentación debe ser una URL completa (https://…).")]
    [StringLength(300)]
    public string? DocumentacionUrl { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }

    public List<int> Productos { get; set; } = [];

    public static ServicioInput Desde(ServicioExterno s) => new()
    {
        Nombre = s.Nombre, Categoria = s.Categoria, Descripcion = s.Descripcion, Estado = s.Estado,
        UrlPanel = s.UrlPanel, CuentaAsociada = s.CuentaAsociada, ResponsableId = s.ResponsableId,
        Plan = s.Plan, Costo = s.Costo, Moneda = s.Moneda, Periodicidad = s.Periodicidad,
        ProximaRenovacion = s.ProximaRenovacion, ReferenciaCredenciales = s.ReferenciaCredenciales,
        DocumentacionUrl = s.DocumentacionUrl, Notas = s.Notas,
        Productos = s.Productos.Select(p => p.ProductoId).ToList()
    };

    public void AplicarA(ServicioExterno s)
    {
        s.Nombre = Nombre.Trim();
        s.Categoria = Categoria;
        s.Descripcion = Opciones.Limpio(Descripcion);
        s.Estado = Estado;
        s.UrlPanel = Opciones.Limpio(UrlPanel);
        s.CuentaAsociada = Opciones.Limpio(CuentaAsociada);
        s.ResponsableId = Opciones.Limpio(ResponsableId);
        s.Plan = Opciones.Limpio(Plan);
        s.Costo = Costo;
        s.Moneda = Moneda;
        s.Periodicidad = Periodicidad;
        s.ProximaRenovacion = ProximaRenovacion;
        s.DiaRenovacion = ProximaRenovacion?.Day;
        s.ReferenciaCredenciales = Opciones.Limpio(ReferenciaCredenciales);
        s.DocumentacionUrl = Opciones.Limpio(DocumentacionUrl);
        s.Notas = Opciones.Limpio(Notas);
    }
}

public class ServicioModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public ServicioInput Input { get; init; } = new();
    public List<Producto> ProductosDisponibles { get; init; } = [];
    public List<OpcionUsuario> Responsables { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Filtro { get; init; }
    public string? Q { get; init; }
}
