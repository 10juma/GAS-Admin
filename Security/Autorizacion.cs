using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace GAS.Admin.Security;

public static class GasClaims
{
    public const string Nombre = "gas:nombre";
    public const string DebeCambiarPassword = "gas:debe_cambiar_password";
}

public sealed record PermisoRequirement(string Permiso) : IAuthorizationRequirement;

/// <summary>El rol Admin tiene todos los permisos; los demás, los que tenga asignados su rol.</summary>
public class PermisoHandler : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (context.User.TienePermiso(requirement.Permiso))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static bool TienePermiso(this ClaimsPrincipal user, string permiso) =>
        user.Identity?.IsAuthenticated == true
        && (user.IsInRole(RolesBase.Admin) || user.HasClaim(Permisos.ClaimType, permiso));

    public static string NombreVisible(this ClaimsPrincipal user) =>
        user.FindFirstValue(GasClaims.Nombre) ?? user.Identity?.Name ?? string.Empty;

    public static string Iniciales(this ClaimsPrincipal user)
    {
        var partes = user.NombreVisible()
            .Split([' ', '.', '@'], StringSplitOptions.RemoveEmptyEntries);
        return partes.Length switch
        {
            0 => "?",
            1 => partes[0][..1].ToUpperInvariant(),
            _ => (partes[0][..1] + partes[1][..1]).ToUpperInvariant()
        };
    }

    public static string? UsuarioId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);
}
