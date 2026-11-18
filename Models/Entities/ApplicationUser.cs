using Microsoft.AspNetCore.Identity;

namespace GAS.Admin.Models.Entities;

/// <summary>Usuario interno de GAS-Admin (independiente de los usuarios de cada producto).</summary>
public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Un usuario desactivado no puede iniciar sesión, pero conserva su historial.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Obliga a cambiar la contraseña temporal en el siguiente acceso.</summary>
    public bool DebeCambiarPassword { get; set; }

    public DateTime FechaAltaUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UltimoAccesoUtc { get; set; }

    public string NombreVisible =>
        string.IsNullOrWhiteSpace(NombreCompleto) ? Email ?? UserName ?? "" : NombreCompleto;
}
