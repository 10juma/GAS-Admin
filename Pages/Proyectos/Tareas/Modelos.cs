using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Proyectos.Tareas;

public class TareaInput
{
    [Required(ErrorMessage = "Ingresa el título de la tarea.")]
    [StringLength(200, ErrorMessage = "El título no puede exceder 200 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "La descripción no puede exceder 4000 caracteres.")]
    public string? Descripcion { get; set; }

    public int? TrabajoId { get; set; }
    public int? EntregableId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? ResponsableId { get; set; }
    public Prioridad Prioridad { get; set; } = Prioridad.Media;
    public EstadoTarea Estado { get; set; } = EstadoTarea.Pendiente;
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaVencimiento { get; set; }

    [Range(0, 999, ErrorMessage = "Las horas estimadas deben estar entre 0 y 999.")]
    public decimal? HorasEstimadas { get; set; }

    public static TareaInput Desde(Tarea t) => new()
    {
        Titulo = t.Titulo, Descripcion = t.Descripcion, TrabajoId = t.TrabajoId, EntregableId = t.EntregableId,
        DepartamentoId = t.DepartamentoId, ResponsableId = t.ResponsableId, Prioridad = t.Prioridad, Estado = t.Estado,
        FechaInicio = t.FechaInicio, FechaVencimiento = t.FechaVencimiento, HorasEstimadas = t.HorasEstimadas
    };

    public void AplicarA(Tarea t)
    {
        t.Titulo = Titulo.Trim();
        t.Descripcion = Opciones.Limpio(Descripcion);
        t.TrabajoId = TrabajoId;
        t.EntregableId = EntregableId;
        t.DepartamentoId = DepartamentoId;
        t.ResponsableId = ResponsableId;
        t.Prioridad = Prioridad;
        if (t.Estado != Estado) t.FechaTerminadaUtc = Estado == EstadoTarea.Terminado ? DateTime.UtcNow : null;
        t.Estado = Estado;
        t.FechaInicio = FechaInicio;
        t.FechaVencimiento = FechaVencimiento;
        t.HorasEstimadas = HorasEstimadas;
    }
}

public sealed record OpcionTrabajo(int Id, string Folio, string Nombre);
public sealed record OpcionEntregable(int Id, int TrabajoId, string Nombre);

public class TareaModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public TareaInput Input { get; init; } = new();
    public List<OpcionTrabajo> Trabajos { get; init; } = [];
    public List<OpcionEntregable> Entregables { get; init; } = [];
    public List<GAS.Admin.Pages.Proyectos.Opcion> Departamentos { get; init; } = [];
    public List<GAS.Admin.Pages.Proyectos.Opcion> Personas { get; init; } = [];
    public List<TareaComentario> Comentarios { get; init; } = [];
    public List<RegistroHoras> Horas { get; init; } = [];
    public List<Adjunto> Archivos { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public bool PuedeComentar { get; init; }
    public bool PuedeRegistrarHoras { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Volver { get; init; }
}
