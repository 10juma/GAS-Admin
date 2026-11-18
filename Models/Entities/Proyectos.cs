using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

public enum TipoTrabajoOperativo { Proyecto, ServicioRecurrente, TrabajoPuntual, ProyectoInterno }

public enum EstadoTrabajo { PorIniciar, EnPlaneacion, EnEjecucion, EnEntrega, Cerrado, EnPausa, Cancelado }

public enum Prioridad { Baja, Media, Alta, Urgente }

/// <summary>Todo lo que GAS vende o se compromete a hacer.</summary>
[DisplayName("Trabajo")]
public class Trabajo : EntidadBase
{
    [Display(Name = "Folio")] public string Folio { get; set; } = string.Empty;
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Tipo")] public TipoTrabajoOperativo Tipo { get; set; } = TipoTrabajoOperativo.Proyecto;
    [Display(Name = "Estado")] public EstadoTrabajo Estado { get; set; } = EstadoTrabajo.PorIniciar;
    [Display(Name = "Prioridad")] public Prioridad Prioridad { get; set; } = Prioridad.Media;
    [Display(Name = "Descripción")] public string? Descripcion { get; set; }
    [Display(Name = "Cliente")] public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Cotización")] public int? CotizacionId { get; set; }
    public Cotizacion? Cotizacion { get; set; }
    [Display(Name = "Departamento")] public int? DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }
    [Display(Name = "Responsable")] public int? ResponsableId { get; set; }
    public Persona? Responsable { get; set; }
    [Display(Name = "Inicio")] public DateOnly? FechaInicio { get; set; }
    [Display(Name = "Entrega comprometida")] public DateOnly? FechaEntrega { get; set; }
    [Display(Name = "Cierre")] public DateOnly? FechaCierre { get; set; }
    /// <summary>Ingreso esperado sin IVA (subtotal de la cotización o capturado).</summary>
    [Display(Name = "Presupuesto")] public decimal? Presupuesto { get; set; }
    [Display(Name = "Moneda")] public string Moneda { get; set; } = "MXN";
    /// <summary>Costo estimado (de la cotización) para comparar contra el real.</summary>
    [Display(Name = "Costo estimado")] public decimal? CostoEstimado { get; set; }
    [Display(Name = "Gastos directos")] public decimal GastosDirectos { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public List<Entregable> Entregables { get; set; } = [];
    public List<Tarea> Tareas { get; set; } = [];
    public List<RegistroHoras> Horas { get; set; } = [];
    public List<CicloTrabajo> Ciclos { get; set; } = [];

    [NotMapped] public bool Activo => Estado is not (EstadoTrabajo.Cerrado or EstadoTrabajo.Cancelado);
    [NotMapped] public bool EsInterno => Tipo == TipoTrabajoOperativo.ProyectoInterno;

    public bool Atrasado(DateOnly hoy) => Activo && Estado != EstadoTrabajo.EnPausa && FechaEntrega < hoy;

    public override string DescripcionAuditoria => $"{Folio} · {Nombre}";
}

public enum EstadoEntregable { Pendiente, Entregado, Aceptado }

[DisplayName("Entregable")]
public class Entregable : EntidadBase
{
    public int TrabajoId { get; set; }
    public Trabajo Trabajo { get; set; } = null!;
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Descripción")] public string? Descripcion { get; set; }
    [Display(Name = "Fecha comprometida")] public DateOnly? FechaCompromiso { get; set; }
    [Display(Name = "Estado")] public EstadoEntregable Estado { get; set; } = EstadoEntregable.Pendiente;
    [Display(Name = "Entregado")] public DateOnly? FechaEntrega { get; set; }
    public int Orden { get; set; }

    public override string DescripcionAuditoria => Nombre;
}

public enum EstadoCiclo { Abierto, Cerrado }

/// <summary>Periodo mensual de un servicio recurrente.</summary>
[DisplayName("Ciclo")]
public class CicloTrabajo : EntidadBase
{
    public int TrabajoId { get; set; }
    public Trabajo Trabajo { get; set; } = null!;
    /// <summary>Primer día del mes del ciclo.</summary>
    [Display(Name = "Periodo")] public DateOnly Periodo { get; set; }
    [Display(Name = "Monto del periodo")] public decimal? Monto { get; set; }
    [Display(Name = "Estado")] public EstadoCiclo Estado { get; set; } = EstadoCiclo.Abierto;
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public override string DescripcionAuditoria => Periodo.ToString("MM/yyyy");
}

public enum EstadoTarea { Pendiente, PorHacer, EnProgreso, EnRevision, EnEspera, Terminado }

[DisplayName("Tarea")]
public class Tarea : EntidadBase
{
    [Display(Name = "Título")] public string Titulo { get; set; } = string.Empty;
    [Display(Name = "Descripción")] public string? Descripcion { get; set; }
    [Display(Name = "Trabajo")] public int? TrabajoId { get; set; }
    public Trabajo? Trabajo { get; set; }
    [Display(Name = "Entregable")] public int? EntregableId { get; set; }
    public Entregable? Entregable { get; set; }
    [Display(Name = "Departamento")] public int? DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }
    [Display(Name = "Responsable")] public int? ResponsableId { get; set; }
    public Persona? Responsable { get; set; }
    [Display(Name = "Prioridad")] public Prioridad Prioridad { get; set; } = Prioridad.Media;
    [Display(Name = "Estado")] public EstadoTarea Estado { get; set; } = EstadoTarea.Pendiente;
    [Display(Name = "Inicio")] public DateOnly? FechaInicio { get; set; }
    [Display(Name = "Vencimiento")] public DateOnly? FechaVencimiento { get; set; }
    [Display(Name = "Horas estimadas")] public decimal? HorasEstimadas { get; set; }
    [Display(Name = "Terminada")] public DateTime? FechaTerminadaUtc { get; set; }
    public int Orden { get; set; }

    public List<TareaComentario> Comentarios { get; set; } = [];
    public List<RegistroHoras> Horas { get; set; } = [];

    public bool Vencida(DateOnly hoy) => Estado != EstadoTarea.Terminado && FechaVencimiento < hoy;

    public override string DescripcionAuditoria => Titulo;
}

/// <summary>Comentario en una tarea (historial de conversación).</summary>
public class TareaComentario
{
    public int Id { get; set; }
    public int TareaId { get; set; }
    public Tarea Tarea { get; set; } = null!;
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    public string Autor { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
}

/// <summary>Horas trabajadas. El costo por hora se congela al registrarlas.</summary>
[DisplayName("Registro de horas")]
public class RegistroHoras : EntidadBase
{
    [Display(Name = "Persona")] public int PersonaId { get; set; }
    public Persona Persona { get; set; } = null!;
    [Display(Name = "Fecha")] public DateOnly Fecha { get; set; }
    [Display(Name = "Horas")] public decimal Horas { get; set; }
    [Display(Name = "Trabajo")] public int? TrabajoId { get; set; }
    public Trabajo? Trabajo { get; set; }
    [Display(Name = "Tarea")] public int? TareaId { get; set; }
    public Tarea? Tarea { get; set; }
    [Display(Name = "Nota")] public string? Nota { get; set; }
    [Display(Name = "Costo por hora")] public decimal? CostoHora { get; set; }
    [Display(Name = "Moneda")] public string? MonedaCosto { get; set; }

    [NotMapped] public decimal? Costo => CostoHora is null ? null : Math.Round(CostoHora.Value * Horas, 2);

    public override string DescripcionAuditoria => $"{Horas:0.##} h · {Fecha:dd/MM/yyyy}";
}

/// <summary>Archivo subido (guardado fuera de wwwroot).</summary>
public class Adjunto
{
    public int Id { get; set; }
    public int? TrabajoId { get; set; }
    public Trabajo? Trabajo { get; set; }
    public int? EntregableId { get; set; }
    public Entregable? Entregable { get; set; }
    public int? TareaId { get; set; }
    public Tarea? Tarea { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    /// <summary>Clave interna en el almacén (nombre aleatorio, sin datos del usuario).</summary>
    public string Clave { get; set; } = string.Empty;
    public string TipoContenido { get; set; } = "application/octet-stream";
    public long Tamano { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    public string? SubidoPor { get; set; }
}
