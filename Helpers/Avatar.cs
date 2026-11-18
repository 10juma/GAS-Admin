namespace GAS.Admin.Helpers;

/// <summary>Iniciales y color estable por persona para los avatares.</summary>
public static class Avatar
{
    private const int Colores = 6;

    // Palabras que no aportan inicial ("Administración y finanzas" → "AF").
    private static readonly HashSet<string> Conectores = new(StringComparer.OrdinalIgnoreCase)
        { "y", "e", "de", "del", "la", "las", "los", "el" };

    public static string Iniciales(string? nombre)
    {
        var partes = (nombre ?? string.Empty)
            .Split([' ', '.', '@', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !Conectores.Contains(p))
            .ToArray();
        return partes.Length switch
        {
            0 => "?",
            1 => partes[0][..1].ToUpperInvariant(),
            _ => (partes[0][..1] + partes[1][..1]).ToUpperInvariant()
        };
    }

    /// <summary>Clase CSS gas-avatar--c1..c6, siempre la misma para el mismo texto.</summary>
    public static string Clase(string? semilla)
    {
        var hash = 0;
        foreach (var c in semilla ?? string.Empty)
        {
            hash = unchecked(hash * 31 + char.ToLowerInvariant(c));
        }
        return $"gas-avatar--c{(Math.Abs(hash % Colores)) + 1}";
    }
}
