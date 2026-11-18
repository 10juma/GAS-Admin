using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

// ===================== Catálogo comercial =====================

public enum ModalidadCobro { PrecioFijo, PorHora, Mensual, PorEntregable, AMedida }

public enum CategoriaComercial
{
    Desarrollo, AppMovil, Implementacion, Personalizacion, Consultoria,
    Analisis, Integracion, Mantenimiento, Soporte, Suscripcion, Otro
}

public enum EstadoCatalogo { Activo, EnDesarrollo, Inactivo }

/// <summary>Producto o servicio que vende GAS. Su precio es de referencia; las cotizaciones copian sus datos.</summary>
[DisplayName("Servicio del catálogo")]
public class ServicioComercial : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Categoría")] public CategoriaComercial Categoria { get; set; } = CategoriaComercial.Otro;
    [Display(Name = "Descripción comercial")] public string? Descripcion { get; set; }
    [Display(Name = "Modalidad de cobro")] public ModalidadCobro Modalidad { get; set; } = ModalidadCobro.PrecioFijo;
    [Display(Name = "Precio base")] public decimal? PrecioBase { get; set; }
    [Display(Name = "Moneda")] public string Moneda { get; set; } = "MXN";
    [Display(Name = "Unidad")] public string? Unidad { get; set; }
    [Display(Name = "Costo estimado")] public decimal? CostoEstimado { get; set; }
    [Display(Name = "Entregables")] public string? Entregables { get; set; }
    [Display(Name = "Tiempo estimado")] public string? TiempoEstimado { get; set; }
    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Estado")] public EstadoCatalogo Estado { get; set; } = EstadoCatalogo.Activo;
    [Display(Name = "Notas")] public string? Notas { get; set; }

    /// <summary>Margen esperado sobre el precio base (0–1), si hay precio y costo.</summary>
    [NotMapped]
    public decimal? Margen => PrecioBase is > 0 && CostoEstimado is not null
        ? Math.Round((PrecioBase.Value - CostoEstimado.Value) / PrecioBase.Value, 4)
        : null;

    public override string DescripcionAuditoria => Nombre;
}

// ===================== Oportunidades =====================

public enum EtapaOportunidad { Nueva, Calificada, PropuestaEnviada, Negociacion, Ganada, Perdida }

public enum OrigenOportunidad { Referido, SitioWeb, RedesSociales, Evento, Prospeccion, ClienteExistente, Otro }

[DisplayName("Oportunidad")]
public class Oportunidad : EntidadBase
{
    [Display(Name = "Título")] public string Titulo { get; set; } = string.Empty;
    [Display(Name = "Cliente")] public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    [Display(Name = "Etapa")] public EtapaOportunidad Etapa { get; set; } = EtapaOportunidad.Nueva;
    [Display(Name = "Valor estimado")] public decimal? ValorEstimado { get; set; }
    [Display(Name = "Moneda")] public string Moneda { get; set; } = "MXN";
    [Display(Name = "Probabilidad")] public int Probabilidad { get; set; } = 10;
    [Display(Name = "Cierre estimado")] public DateOnly? FechaCierreEstimada { get; set; }
    [Display(Name = "Cerrada")] public DateTime? FechaCierreUtc { get; set; }
    [Display(Name = "Origen")] public OrigenOportunidad Origen { get; set; } = OrigenOportunidad.Otro;
    [Display(Name = "Producto de interés")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Responsable")] public string? ResponsableId { get; set; }
    public ApplicationUser? Responsable { get; set; }
    [Display(Name = "Motivo de pérdida")] public string? MotivoPerdida { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }
    /// <summary>Posición dentro de su columna del Kanban.</summary>
    public int Orden { get; set; }

    public List<Cotizacion> Cotizaciones { get; set; } = [];

    [NotMapped] public bool Abierta => Etapa is not (EtapaOportunidad.Ganada or EtapaOportunidad.Perdida);
    [NotMapped] public decimal? ValorPonderado => ValorEstimado is null ? null : Math.Round(ValorEstimado.Value * Probabilidad / 100m, 2);

    public static int ProbabilidadSugerida(EtapaOportunidad etapa) => etapa switch
    {
        EtapaOportunidad.Nueva => 10,
        EtapaOportunidad.Calificada => 25,
        EtapaOportunidad.PropuestaEnviada => 50,
        EtapaOportunidad.Negociacion => 75,
        EtapaOportunidad.Ganada => 100,
        _ => 0
    };

    public override string DescripcionAuditoria => Titulo;
}

// ===================== Cotizaciones =====================

public enum EstadoCotizacion { Borrador, Enviada, Aprobada, Rechazada, Cancelada, Reemplazada }

public enum CondicionPago { Contado, AnticipoYFinal, Hitos, Mensual }

public enum TipoTrabajo { Proyecto, ServicioRecurrente, TrabajoPuntual }

[DisplayName("Cotización")]
public class Cotizacion : EntidadBase
{
    /// <summary>Folio base, por ejemplo COT-2026-0001. Las versiones comparten folio.</summary>
    [Display(Name = "Folio")] public string Folio { get; set; } = string.Empty;
    [Display(Name = "Versión")] public int Version { get; set; } = 1;
    [Display(Name = "Título")] public string Titulo { get; set; } = string.Empty;
    [Display(Name = "Cliente")] public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    [Display(Name = "Oportunidad")] public int? OportunidadId { get; set; }
    public Oportunidad? Oportunidad { get; set; }
    [Display(Name = "Estado")] public EstadoCotizacion Estado { get; set; } = EstadoCotizacion.Borrador;
    [Display(Name = "Moneda")] public string Moneda { get; set; } = "MXN";
    [Display(Name = "Fecha de emisión")] public DateOnly FechaEmision { get; set; }
    [Display(Name = "Vigencia (días)")] public int VigenciaDias { get; set; } = 15;
    [Display(Name = "Tasa de IVA")] public decimal TasaIva { get; set; } = 0.16m;
    [Display(Name = "Subtotal")] public decimal Subtotal { get; set; }
    [Display(Name = "Descuento")] public decimal Descuento { get; set; }
    [Display(Name = "IVA")] public decimal Iva { get; set; }
    [Display(Name = "Total")] public decimal Total { get; set; }
    [Display(Name = "Costo estimado")] public decimal? CostoEstimado { get; set; }
    [Display(Name = "Condición de pago")] public CondicionPago CondicionPago { get; set; } = CondicionPago.AnticipoYFinal;
    [Display(Name = "Anticipo (%)")] public int? AnticipoPorcentaje { get; set; } = 50;
    [Display(Name = "Condiciones")] public string? Condiciones { get; set; }
    [Display(Name = "Notas internas")] public string? NotasInternas { get; set; }
    [Display(Name = "Responsable")] public string? ResponsableId { get; set; }
    public ApplicationUser? Responsable { get; set; }
    [Display(Name = "Enviada")] public DateTime? FechaEnvioUtc { get; set; }
    [Display(Name = "Aprobada")] public DateTime? FechaAprobacionUtc { get; set; }
    [Display(Name = "Tipo de trabajo")] public TipoTrabajo? TipoTrabajo { get; set; }

    public List<CotizacionLinea> Lineas { get; set; } = [];
    public List<CotizacionEnvio> Envios { get; set; } = [];

    [NotMapped] public string FolioCompleto => Version > 1 ? $"{Folio} R{Version}" : Folio;
    [NotMapped] public DateOnly FechaVencimiento => FechaEmision.AddDays(VigenciaDias);
    [NotMapped] public bool Editable => Estado == EstadoCotizacion.Borrador;
    [NotMapped] public decimal? Margen => Subtotal > 0 && CostoEstimado is not null
        ? Math.Round((Subtotal - CostoEstimado.Value) / Subtotal, 4) : null;

    /// <summary>Una cotización enviada cuya vigencia ya pasó se considera vencida.</summary>
    public bool Vencida(DateOnly hoy) => Estado == EstadoCotizacion.Enviada && FechaVencimiento < hoy;

    /// <summary>Recalcula importes de cada línea y los totales.</summary>
    public void Recalcular()
    {
        decimal bruto = 0, subtotal = 0, costo = 0;
        var hayCosto = false;
        foreach (var l in Lineas)
        {
            var lineaBruta = Math.Round(l.Cantidad * l.PrecioUnitario, 2);
            l.Importe = Math.Round(lineaBruta * (1 - l.DescuentoPorcentaje / 100m), 2);
            bruto += lineaBruta;
            subtotal += l.Importe;
            if (l.CostoUnitario is not null)
            {
                costo += Math.Round(l.Cantidad * l.CostoUnitario.Value, 2);
                hayCosto = true;
            }
        }
        Subtotal = subtotal;
        Descuento = bruto - subtotal;
        Iva = Math.Round(subtotal * TasaIva, 2);
        Total = Subtotal + Iva;
        CostoEstimado = hayCosto ? costo : null;
    }

    public override string DescripcionAuditoria => $"{FolioCompleto} · {Titulo}";
}

/// <summary>Concepto de una cotización. Es una copia: no cambia si cambia el catálogo.</summary>
public class CotizacionLinea
{
    public int Id { get; set; }
    public int CotizacionId { get; set; }
    public Cotizacion Cotizacion { get; set; } = null!;
    public int Orden { get; set; }
    /// <summary>Servicio del catálogo del que se copió (solo referencia).</summary>
    public int? ServicioComercialId { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Cantidad { get; set; } = 1;
    public string? Unidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal DescuentoPorcentaje { get; set; }
    public decimal? CostoUnitario { get; set; }
    public decimal Importe { get; set; }
}

/// <summary>Registro de cada envío por correo.</summary>
public class CotizacionEnvio
{
    public int Id { get; set; }
    public int CotizacionId { get; set; }
    public Cotizacion Cotizacion { get; set; } = null!;
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    public string Para { get; set; } = string.Empty;
    public string? Copia { get; set; }
    public string Asunto { get; set; } = string.Empty;
    public string? EnviadoPor { get; set; }
    public bool Exito { get; set; }
    public string? IdProveedor { get; set; }
    public string? Error { get; set; }
}
