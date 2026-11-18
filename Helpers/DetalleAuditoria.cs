using System.Security.Claims;
using System.Text.RegularExpressions;
using GAS.Admin.Security;

namespace GAS.Admin.Helpers;

/// <summary>
/// Montos en el detalle de la bitácora: el interceptor los guarda entre ⟦ ⟧ y al mostrarlos
/// se ocultan a quien no tiene Finanzas.Ver.
/// </summary>
public static partial class DetalleAuditoria
{
    private const char Abre = '⟦', Cierra = '⟧';

    public static string Marcar(string texto) => $"{Abre}{texto}{Cierra}";

    [GeneratedRegex("⟦[^⟧]*⟧")]
    private static partial Regex Montos();

    public static string Visible(string? detalle, bool verMontos)
    {
        if (string.IsNullOrEmpty(detalle)) return "";
        return verMontos
            ? detalle.Replace(Abre.ToString(), "").Replace(Cierra.ToString(), "")
            : Montos().Replace(detalle, "(monto oculto)");
    }

    public static string Visible(string? detalle, ClaimsPrincipal usuario) =>
        Visible(detalle, usuario.TienePermiso(Permisos.Finanzas.Ver));
}
