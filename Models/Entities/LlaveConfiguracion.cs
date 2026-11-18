namespace GAS.Admin.Models.Entities;

/// <summary>
/// Llave o ajuste capturado desde GAS-Admin (Resend, Stripe, integraciones, secretos de productos).
/// El valor siempre va cifrado con la llave maestra (Seguridad:LlaveMaestra), que vive fuera de la BD.
/// No hereda EntidadBase para que la auditoría automática nunca vea el valor; los cambios se registran a mano.
/// </summary>
public class LlaveConfiguracion
{
    public int Id { get; set; }
    /// <summary>Ruta de configuración, p. ej. "Resend:ApiKey" o "Stripe:Claves:revendo".</summary>
    public string Clave { get; set; } = string.Empty;
    public string ValorCifrado { get; set; } = string.Empty;
    /// <summary>Secreto: nunca se vuelve a mostrar, solo <see cref="Pista"/>. Ajuste (remitente, organización): se muestra.</summary>
    public bool EsSecreto { get; set; } = true;
    /// <summary>Últimos 4 caracteres del secreto, para reconocerlo.</summary>
    public string? Pista { get; set; }
    public DateTime ModificadoUtc { get; set; } = DateTime.UtcNow;
    public string? ModificadoPor { get; set; }
}
