using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

// Fase 7: atención al cliente. Tickets que llegan por captura interna, desde los productos
// (API GAS) o por correo; respuestas por correo; SLA por prioridad; base de conocimientos.

public enum EstadoTicket { Nuevo, Abierto, EnEspera, Escalado, Resuelto, Cerrado }

public enum CategoriaTicket { Duda, Problema, Solicitud, Facturacion, Sugerencia }

public enum CanalTicket { Interno, Producto, Correo }

public enum TipoMensajeTicket { Cliente, Respuesta, Nota, Sistema }

[DisplayName("Ticket")]
public class Ticket : EntidadBase
{
    [Display(Name = "Folio")] public string Folio { get; set; } = string.Empty;
    [Display(Name = "Asunto")] public string Asunto { get; set; } = string.Empty;
    [Display(Name = "Estado")] public EstadoTicket Estado { get; set; } = EstadoTicket.Nuevo;
    [Display(Name = "Prioridad")] public Prioridad Prioridad { get; set; } = Prioridad.Media;
    [Display(Name = "Categoría")] public CategoriaTicket Categoria { get; set; } = CategoriaTicket.Duda;
    [Display(Name = "Canal")] public CanalTicket Canal { get; set; } = CanalTicket.Interno;

    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Cliente")] public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    [Display(Name = "Cuenta del producto")] public int? CuentaProductoId { get; set; }
    public CuentaProducto? CuentaProducto { get; set; }
    [Display(Name = "Contacto")] public string? ContactoNombre { get; set; }
    [Display(Name = "Correo del contacto")] public string? ContactoEmail { get; set; }

    [Display(Name = "Asignado a")] public string? AsignadoId { get; set; }
    public ApplicationUser? Asignado { get; set; }

    [Display(Name = "Vence primera respuesta")] public DateTime? VencePrimeraRespuestaUtc { get; set; }
    [Display(Name = "Vence resolución")] public DateTime? VenceResolucionUtc { get; set; }
    [Display(Name = "Primera respuesta")] public DateTime? PrimeraRespuestaUtc { get; set; }
    [Display(Name = "Resuelto")] public DateTime? ResueltoUtc { get; set; }
    [Display(Name = "Última actividad")] public DateTime UltimaActividadUtc { get; set; } = DateTime.UtcNow;

    [Display(Name = "Tarea en Proyectos")] public int? TareaId { get; set; }
    public Tarea? Tarea { get; set; }
    /// <summary>Id en el sistema de origen (ticket del producto o Message-ID del correo).</summary>
    [Display(Name = "Referencia externa")] public string? ReferenciaExterna { get; set; }

    public List<TicketMensaje> Mensajes { get; set; } = [];

    [NotMapped] public bool Abierto => Estado is not (EstadoTicket.Resuelto or EstadoTicket.Cerrado);

    public bool PrimeraRespuestaVencida(DateTime ahora) => Abierto && PrimeraRespuestaUtc is null && VencePrimeraRespuestaUtc < ahora;
    public bool ResolucionVencida(DateTime ahora) => Abierto && VenceResolucionUtc < ahora;
    public bool SlaVencido(DateTime ahora) => PrimeraRespuestaVencida(ahora) || ResolucionVencida(ahora);

    public override string DescripcionAuditoria => $"{Folio} · {Asunto}";
}

public class TicketMensaje
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public TipoMensajeTicket Tipo { get; set; }
    public string Autor { get; set; } = string.Empty;
    public string? AutorEmail { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Para respuestas: si el correo salió, su id y el error si falló.</summary>
    public bool? CorreoEnviado { get; set; }
    public string? CorreoId { get; set; }
    public string? CorreoError { get; set; }
    /// <summary>
    /// Mensaje que llegó de un producto (API GAS): "{clave}:{id del mensaje en el producto}". Hace el envío
    /// idempotente y le dice al producto cuáles mensajes ya tiene (no los vuelve a importar).
    /// </summary>
    public string? ReferenciaExterna { get; set; }
}

/// <summary>Tiempos de servicio por prioridad (horas naturales).</summary>
public class SlaPrioridad
{
    public Prioridad Prioridad { get; set; }
    public int PrimeraRespuestaHoras { get; set; }
    public int ResolucionHoras { get; set; }
}

[DisplayName("Artículo")]
public class ArticuloConocimiento : EntidadBase
{
    [Display(Name = "Título")] public string Titulo { get; set; } = string.Empty;
    [Display(Name = "Contenido")] public string Contenido { get; set; } = string.Empty;
    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Categoría")] public CategoriaTicket Categoria { get; set; } = CategoriaTicket.Duda;
    [Display(Name = "Etiquetas")] public string? Etiquetas { get; set; }
    /// <summary>Se podrá mostrar a clientes en cada producto (API GAS, más adelante).</summary>
    [Display(Name = "Publicable")] public bool Publicable { get; set; }
    [Display(Name = "Usos en respuestas")] public int Usos { get; set; }

    public override string DescripcionAuditoria => Titulo;
}
