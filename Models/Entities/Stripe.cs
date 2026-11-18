using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

// Fase 4: Stripe es la fuente de verdad de suscripciones y pagos. GAS-Admin solo consulta
// (con una API key restringida de solo lectura por producto) y guarda una copia para métricas.
// Las tablas espejo no heredan de EntidadBase para no llenar la auditoría en cada sincronización.

/// <summary>Conexión de un producto con su cuenta de Stripe. La API key vive en configuración, nunca aquí.</summary>
[DisplayName("Cuenta de Stripe")]
public class CuentaStripe : EntidadBase
{
    [Display(Name = "Producto")] public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    /// <summary>Clave de configuración: la API key se lee de "Stripe:Claves:{Clave}".</summary>
    [Display(Name = "Clave de configuración")] public string Clave { get; set; } = string.Empty;
    [Display(Name = "Precios incluyen IVA")] public bool PreciosIncluyenIva { get; set; } = true;
    [Display(Name = "Activa")] public bool Activa { get; set; } = true;
    [Display(Name = "Cuenta de Stripe")] public string? CuentaId { get; set; }
    [Display(Name = "Nombre en Stripe")] public string? NombreStripe { get; set; }
    [Display(Name = "Modo de prueba")] public bool ModoPrueba { get; set; }
    [Display(Name = "Última sincronización")] public DateTime? UltimaSincronizacionUtc { get; set; }
    [Display(Name = "Último resultado")] public string? UltimoResultado { get; set; }
    [Display(Name = "Último error")] public string? UltimoError { get; set; }

    public override string DescripcionAuditoria => $"Stripe · {Clave}";
}

public class SuscriptorStripe
{
    public int Id { get; set; }
    public int CuentaStripeId { get; set; }
    public CuentaStripe Cuenta { get; set; } = null!;
    public string StripeId { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Email { get; set; }
    /// <summary>Liga opcional a un cliente de GAS (cuando hay relación comercial).</summary>
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public DateTime? CreadoStripeUtc { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;

    public List<SuscripcionStripe> Suscripciones { get; set; } = [];

    [NotMapped] public string Visible => Nombre ?? Email ?? StripeId;
}

public class SuscripcionStripe
{
    public int Id { get; set; }
    public int CuentaStripeId { get; set; }
    public CuentaStripe Cuenta { get; set; } = null!;
    public string StripeId { get; set; } = string.Empty;
    public int? SuscriptorId { get; set; }
    public SuscriptorStripe? Suscriptor { get; set; }
    /// <summary>Estado tal como lo reporta Stripe: active, trialing, past_due, canceled, unpaid, incomplete, paused…</summary>
    public string Estado { get; set; } = string.Empty;
    public string? Plan { get; set; }
    public string Moneda { get; set; } = "MXN";
    /// <summary>Importe recurrente normalizado a un mes, en su moneda.</summary>
    public decimal MontoMensual { get; set; }
    public string? Intervalo { get; set; }
    public int IntervaloCantidad { get; set; } = 1;
    public int Cantidad { get; set; } = 1;
    public DateTime? InicioUtc { get; set; }
    public DateTime? PeriodoFinUtc { get; set; }
    public DateTime? PruebaHastaUtc { get; set; }
    public bool CancelarAlFinal { get; set; }
    public DateTime? CanceladaUtc { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Cuenta para el MRR: activa o con pago atrasado (no las de prueba ni canceladas).</summary>
    [NotMapped] public bool CuentaEnMrr => Estado is "active" or "past_due";
    [NotMapped] public bool Vigente => Estado is "active" or "past_due" or "trialing" or "unpaid" or "paused";
}

public class FacturaStripe
{
    public int Id { get; set; }
    public int CuentaStripeId { get; set; }
    public CuentaStripe Cuenta { get; set; } = null!;
    public string StripeId { get; set; } = string.Empty;
    public string? Numero { get; set; }
    public int? SuscriptorId { get; set; }
    public SuscriptorStripe? Suscriptor { get; set; }
    public int? SuscripcionId { get; set; }
    public SuscripcionStripe? Suscripcion { get; set; }
    /// <summary>draft, open, paid, uncollectible o void.</summary>
    public string Estado { get; set; } = string.Empty;
    public string Moneda { get; set; } = "MXN";
    public decimal Total { get; set; }
    public decimal Impuestos { get; set; }
    public decimal Pagado { get; set; }
    /// <summary>Comisión de Stripe del cobro (en la moneda de liquidación).</summary>
    public decimal? Comision { get; set; }
    public string? MonedaComision { get; set; }
    public DateTime FechaUtc { get; set; }
    public DateTime? PagadaUtc { get; set; }
    public int Intentos { get; set; }
    public DateTime? ProximoIntentoUtc { get; set; }
    public string? Url { get; set; }
    public int? MovimientoIngresoId { get; set; }
    public int? MovimientoComisionId { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Cobro fallido: abierta con al menos un intento, o marcada como incobrable.</summary>
    [NotMapped] public bool Fallida => (Estado == "open" && Intentos > 0) || Estado == "uncollectible";
}

/// <summary>Foto diaria de las métricas de una cuenta (para la tendencia del MRR).</summary>
public class MetricaStripe
{
    public int Id { get; set; }
    public int CuentaStripeId { get; set; }
    public CuentaStripe Cuenta { get; set; } = null!;
    public DateOnly Fecha { get; set; }
    public string Moneda { get; set; } = "MXN";
    public decimal Mrr { get; set; }
    public int Activas { get; set; }
    public int EnPrueba { get; set; }
    public int Atrasadas { get; set; }
}
