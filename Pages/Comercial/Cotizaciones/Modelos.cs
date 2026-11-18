using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Comercial.Cotizaciones;

public class LineaInput
{
    public int? ServicioComercialId { get; set; }

    [Required(ErrorMessage = "Cada concepto necesita un nombre.")]
    [StringLength(200, ErrorMessage = "El concepto no puede exceder 200 caracteres.")]
    public string Concepto { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "La descripción de un concepto no puede exceder 1000 caracteres.")]
    public string? Descripcion { get; set; }

    [Range(0.01, 1_000_000, ErrorMessage = "La cantidad debe ser mayor a cero.")]
    public decimal Cantidad { get; set; } = 1;

    [StringLength(50)]
    public string? Unidad { get; set; }

    [Range(0, 999_999_999, ErrorMessage = "El precio debe ser un número positivo.")]
    public decimal PrecioUnitario { get; set; }

    [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0 y 100 %.")]
    public decimal DescuentoPorcentaje { get; set; }

    [Range(0, 999_999_999, ErrorMessage = "El costo debe ser un número positivo.")]
    public decimal? CostoUnitario { get; set; }

    public static LineaInput Desde(CotizacionLinea l) => new()
    {
        ServicioComercialId = l.ServicioComercialId, Concepto = l.Concepto, Descripcion = l.Descripcion,
        Cantidad = l.Cantidad, Unidad = l.Unidad, PrecioUnitario = l.PrecioUnitario,
        DescuentoPorcentaje = l.DescuentoPorcentaje, CostoUnitario = l.CostoUnitario
    };

    public CotizacionLinea ALinea(int orden) => new()
    {
        Orden = orden, ServicioComercialId = ServicioComercialId, Concepto = Concepto.Trim(),
        Descripcion = Opciones.Limpio(Descripcion), Cantidad = Cantidad, Unidad = Opciones.Limpio(Unidad),
        PrecioUnitario = PrecioUnitario, DescuentoPorcentaje = DescuentoPorcentaje, CostoUnitario = CostoUnitario
    };
}

public class CotizacionInput
{
    public static readonly string[] Monedas = ["MXN", "USD"];

    [Required(ErrorMessage = "Ingresa un título para la cotización.")]
    [StringLength(200, ErrorMessage = "El título no puede exceder 200 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    public int? ClienteId { get; set; }
    public int? OportunidadId { get; set; }
    public string Moneda { get; set; } = "MXN";
    public DateOnly FechaEmision { get; set; }

    [Range(1, 365, ErrorMessage = "La vigencia debe estar entre 1 y 365 días.")]
    public int VigenciaDias { get; set; } = 15;

    [Range(0, 100, ErrorMessage = "El IVA debe estar entre 0 y 100 %.")]
    public decimal TasaIvaPorcentaje { get; set; } = 16;

    public CondicionPago CondicionPago { get; set; } = CondicionPago.AnticipoYFinal;

    [Range(1, 99, ErrorMessage = "El anticipo debe estar entre 1 y 99 %.")]
    public int? AnticipoPorcentaje { get; set; } = 50;

    [StringLength(4000, ErrorMessage = "Las condiciones no pueden exceder 4000 caracteres.")]
    public string? Condiciones { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas internas no pueden exceder 2000 caracteres.")]
    public string? NotasInternas { get; set; }

    public string? ResponsableId { get; set; }

    public List<LineaInput> Lineas { get; set; } = [];

    public static CotizacionInput Desde(Cotizacion c) => new()
    {
        Titulo = c.Titulo, ClienteId = c.ClienteId, OportunidadId = c.OportunidadId, Moneda = c.Moneda,
        FechaEmision = c.FechaEmision, VigenciaDias = c.VigenciaDias, TasaIvaPorcentaje = c.TasaIva * 100,
        CondicionPago = c.CondicionPago, AnticipoPorcentaje = c.AnticipoPorcentaje, Condiciones = c.Condiciones,
        NotasInternas = c.NotasInternas, ResponsableId = c.ResponsableId,
        Lineas = c.Lineas.OrderBy(l => l.Orden).Select(LineaInput.Desde).ToList()
    };

    public void AplicarA(Cotizacion c)
    {
        c.Titulo = Titulo.Trim();
        c.ClienteId = ClienteId!.Value;
        c.OportunidadId = OportunidadId;
        c.Moneda = Moneda;
        c.FechaEmision = FechaEmision;
        c.VigenciaDias = VigenciaDias;
        c.TasaIva = Math.Round(TasaIvaPorcentaje / 100m, 4);
        c.CondicionPago = CondicionPago;
        c.AnticipoPorcentaje = CondicionPago == CondicionPago.AnticipoYFinal ? AnticipoPorcentaje : null;
        c.Condiciones = Opciones.Limpio(Condiciones);
        c.NotasInternas = Opciones.Limpio(NotasInternas);
        c.ResponsableId = Opciones.Limpio(ResponsableId);

        c.Lineas.Clear();
        for (var i = 0; i < Lineas.Count; i++) c.Lineas.Add(Lineas[i].ALinea(i));
        c.Recalcular();
    }
}

public sealed record OpcionOportunidad(int Id, int ClienteId, string Titulo, string Cliente);
public sealed record ItemCatalogo(int Id, string Nombre, string? Descripcion, decimal? Precio, string Moneda, string? Unidad, decimal? Costo, string Categoria);

public class CotizacionModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public string? Folio { get; init; }
    public EstadoCotizacion Estado { get; init; } = EstadoCotizacion.Borrador;
    public bool Vencida { get; init; }
    public bool Editable => PuedeGestionar && Estado == EstadoCotizacion.Borrador;
    public CotizacionInput Input { get; init; } = new();
    public List<GAS.Admin.Pages.Comercial.Oportunidades.OpcionCliente> Clientes { get; init; } = [];
    public List<OpcionOportunidad> Oportunidades { get; init; } = [];
    public List<OpcionUsuario> Responsables { get; init; } = [];
    public List<ItemCatalogo> Catalogo { get; init; } = [];
    public List<CotizacionEnvio> Envios { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public bool PuedeEliminar { get; init; }
    /// <summary>Trabajo creado a partir de esta cotización (Id y Folio en Titulo).</summary>
    public OpcionOportunidad? Trabajo { get; init; }
    public bool PuedeCrearTrabajo { get; init; }
    public TipoTrabajo? TipoTrabajo { get; init; }
    public DateTime? FechaAprobacionUtc { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Filtro { get; init; }
    public string? Q { get; init; }
}

public class EnvioInput
{
    [Required(ErrorMessage = "Indica a quién se envía.")]
    [StringLength(500)]
    public string Para { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Copia { get; set; }

    [Required(ErrorMessage = "El asunto es obligatorio.")]
    [StringLength(300, ErrorMessage = "El asunto no puede exceder 300 caracteres.")]
    public string Asunto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe un mensaje.")]
    [StringLength(4000, ErrorMessage = "El mensaje no puede exceder 4000 caracteres.")]
    public string Mensaje { get; set; } = string.Empty;
}

public class EnvioModalVm
{
    public int Id { get; init; }
    public string Folio { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string Cliente { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string Moneda { get; init; } = "MXN";
    public EnvioInput Input { get; init; } = new();
    public bool Configurado { get; init; }
    public string? Remitente { get; init; }
    public List<CotizacionEnvio> Envios { get; init; } = [];
    public string? Filtro { get; init; }
    public string? Q { get; init; }
}
