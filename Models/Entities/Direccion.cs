namespace GAS.Admin.Models.Entities;

/// <summary>
/// Foto de los indicadores de un mes. Los indicadores del periodo se pueden recalcular;
/// los puntuales (MRR, por cobrar, cuentas activas…) solo valen en el momento en que se captura.
/// </summary>
public class CierreMensual
{
    /// <summary>Primer día del mes.</summary>
    public DateOnly Mes { get; set; }
    public DateTime CapturadoUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Parcial = reconstruido después: sin indicadores puntuales.</summary>
    public bool Parcial { get; set; }
    /// <summary>Indicadores en JSON: { "clave": valor }.</summary>
    public string IndicadoresJson { get; set; } = "{}";
}
