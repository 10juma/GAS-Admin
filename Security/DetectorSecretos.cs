using System.Text.RegularExpressions;

namespace GAS.Admin.Security;

/// <summary>
/// Evita guardar contraseñas o API keys donde solo debe ir una referencia
/// (por ejemplo "1Password › GAS › Stripe").
/// </summary>
public static partial class DetectorSecretos
{
    [GeneratedRegex(
        @"(sk|rk|pk)_(live|test)_[A-Za-z0-9]{8,}|whsec_[A-Za-z0-9]{8,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[abprs]-[A-Za-z0-9-]{10,}|AKIA[0-9A-Z]{16}|re_[A-Za-z0-9_]{20,}|AIza[0-9A-Za-z_\-]{30,}|-----BEGIN|(password|contrase(ñ|n)a|pwd|secret|token|api[_ ]?key)\s*[:=]\s*\S+",
        RegexOptions.IgnoreCase)]
    private static partial Regex Patrones();

    // Cadena larga sin espacios con mezcla de letras y números: probablemente una clave.
    [GeneratedRegex(@"\b(?=[A-Za-z0-9_\-]*\d)(?=[A-Za-z0-9_\-]*[A-Za-z])[A-Za-z0-9_\-]{32,}\b")]
    private static partial Regex TokenLargo();

    public static bool PareceSecreto(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) && (Patrones().IsMatch(texto) || TokenLargo().IsMatch(texto));
}
