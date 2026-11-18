using GAS.Admin.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Middleware;

/// <summary>
/// Si el usuario tiene una contraseña temporal, solo le permite entrar a
/// "Cambiar contraseña" y "Cerrar sesión" hasta que la cambie.
/// </summary>
public class CambioPasswordObligatorioMiddleware
{
    private static readonly string[] RutasPermitidas =
    [
        "/Account/ChangePassword",
        "/Account/Logout",
        "/Account/Login",
        "/Error"
    ];

    private readonly RequestDelegate _next;

    public CambioPasswordObligatorioMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var debeCambiar = context.User.HasClaim(GasClaims.DebeCambiarPassword, "true");
        var esPagina = context.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>() is not null;

        if (debeCambiar && esPagina
            && !RutasPermitidas.Any(r => context.Request.Path.StartsWithSegments(r, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Redirect("/Account/ChangePassword");
            return;
        }

        await _next(context);
    }
}
