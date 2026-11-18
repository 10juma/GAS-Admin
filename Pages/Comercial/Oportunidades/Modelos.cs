using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Comercial.Oportunidades;

public class OportunidadInput
{
    public static readonly string[] Monedas = ["MXN", "USD"];

    [Required(ErrorMessage = "Ingresa un título para la oportunidad.")]
    [StringLength(200, ErrorMessage = "El título no puede exceder 200 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    public int? ClienteId { get; set; }

    /// <summary>Si se escribe, se crea un cliente nuevo en estado Prospecto.</summary>
    [StringLength(200, ErrorMessage = "El nombre del prospecto no puede exceder 200 caracteres.")]
    public string? NuevoProspecto { get; set; }

    public EtapaOportunidad Etapa { get; set; } = EtapaOportunidad.Nueva;

    [Range(0, 999_999_999, ErrorMessage = "El valor estimado debe ser un número positivo.")]
    public decimal? ValorEstimado { get; set; }

    public string Moneda { get; set; } = "MXN";

    [Range(0, 100, ErrorMessage = "La probabilidad debe estar entre 0 y 100.")]
    public int Probabilidad { get; set; } = 10;

    public DateOnly? FechaCierreEstimada { get; set; }
    public OrigenOportunidad Origen { get; set; } = OrigenOportunidad.Otro;
    public int? ProductoId { get; set; }
    public string? ResponsableId { get; set; }

    [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres.")]
    public string? MotivoPerdida { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }

    public static OportunidadInput Desde(Oportunidad o) => new()
    {
        Titulo = o.Titulo, ClienteId = o.ClienteId, Etapa = o.Etapa, ValorEstimado = o.ValorEstimado,
        Moneda = o.Moneda, Probabilidad = o.Probabilidad, FechaCierreEstimada = o.FechaCierreEstimada,
        Origen = o.Origen, ProductoId = o.ProductoId, ResponsableId = o.ResponsableId,
        MotivoPerdida = o.MotivoPerdida, Notas = o.Notas
    };

    public void AplicarA(Oportunidad o)
    {
        o.Titulo = Titulo.Trim();
        if (ClienteId is not null) o.ClienteId = ClienteId.Value;
        o.ValorEstimado = ValorEstimado;
        o.Moneda = Moneda;
        o.Probabilidad = Probabilidad;
        o.FechaCierreEstimada = FechaCierreEstimada;
        o.Origen = Origen;
        o.ProductoId = ProductoId;
        o.ResponsableId = Opciones.Limpio(ResponsableId);
        o.MotivoPerdida = Etapa == EtapaOportunidad.Perdida ? Opciones.Limpio(MotivoPerdida) : null;
        o.Notas = Opciones.Limpio(Notas);
    }
}

public sealed record OpcionCliente(int Id, string Nombre, EstadoCliente Estado);

public sealed record CotizacionResumen(int Id, string Folio, string Titulo, EstadoCotizacion Estado, decimal Total, string Moneda);

public class OportunidadModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public OportunidadInput Input { get; init; } = new();
    public List<OpcionCliente> Clientes { get; init; } = [];
    public List<Producto> Productos { get; init; } = [];
    public List<OpcionUsuario> Responsables { get; init; } = [];
    public List<CotizacionResumen> Cotizaciones { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Vista { get; init; }
}

/// <summary>Etapa llega como texto ("Calificada") desde el Kanban.</summary>
public sealed record MoverRequest(int Id, string Etapa, List<int>? Ids);
