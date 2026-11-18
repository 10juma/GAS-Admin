using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Proyectos;

public class TrabajoInput
{
    public static readonly string[] Monedas = ["MXN", "USD"];

    [Required(ErrorMessage = "Ingresa el nombre del trabajo.")]
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public TipoTrabajoOperativo Tipo { get; set; } = TipoTrabajoOperativo.Proyecto;
    public EstadoTrabajo Estado { get; set; } = EstadoTrabajo.PorIniciar;
    public Prioridad Prioridad { get; set; } = Prioridad.Media;

    [StringLength(2000, ErrorMessage = "La descripción no puede exceder 2000 caracteres.")]
    public string? Descripcion { get; set; }

    public int? ClienteId { get; set; }
    public int? ProductoId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? ResponsableId { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaEntrega { get; set; }

    [Range(0, 999_999_999, ErrorMessage = "El presupuesto debe ser un número positivo.")]
    public decimal? Presupuesto { get; set; }

    public string Moneda { get; set; } = "MXN";

    [Range(0, 999_999_999, ErrorMessage = "Los gastos directos deben ser un número positivo.")]
    public decimal GastosDirectos { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }

    public static TrabajoInput Desde(Trabajo t) => new()
    {
        Nombre = t.Nombre, Tipo = t.Tipo, Estado = t.Estado, Prioridad = t.Prioridad, Descripcion = t.Descripcion,
        ClienteId = t.ClienteId, ProductoId = t.ProductoId, DepartamentoId = t.DepartamentoId, ResponsableId = t.ResponsableId,
        FechaInicio = t.FechaInicio, FechaEntrega = t.FechaEntrega, Presupuesto = t.Presupuesto, Moneda = t.Moneda,
        GastosDirectos = t.GastosDirectos, Notas = t.Notas
    };

    public void AplicarA(Trabajo t, bool editarFinanzas)
    {
        t.Nombre = Nombre.Trim();
        t.Tipo = Tipo;
        if (t.Estado != Estado)
        {
            t.FechaCierre = Estado is EstadoTrabajo.Cerrado or EstadoTrabajo.Cancelado
                ? Services.Proyectos.ServicioTrabajos.Hoy : null;
        }
        t.Estado = Estado;
        t.Prioridad = Prioridad;
        t.Descripcion = Opciones.Limpio(Descripcion);
        t.ClienteId = Tipo == TipoTrabajoOperativo.ProyectoInterno ? null : ClienteId;
        t.ProductoId = ProductoId;
        t.DepartamentoId = DepartamentoId;
        t.ResponsableId = ResponsableId;
        t.FechaInicio = FechaInicio;
        t.FechaEntrega = FechaEntrega;
        t.Notas = Opciones.Limpio(Notas);
        if (editarFinanzas)
        {
            t.Presupuesto = Presupuesto;
            t.Moneda = Moneda;
            t.GastosDirectos = GastosDirectos;
        }
    }
}

public sealed record Opcion(int Id, string Nombre);

public class TrabajoModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public string? Folio { get; init; }
    public string? Cotizacion { get; init; }
    public int? CotizacionId { get; init; }
    public TrabajoInput Input { get; init; } = new();
    public List<Opcion> Clientes { get; init; } = [];
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Departamentos { get; init; } = [];
    public List<Opcion> Personas { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public bool VerFinanzas { get; init; }
    public bool EditarFinanzas { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Volver { get; init; }
}
