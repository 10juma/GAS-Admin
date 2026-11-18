using GAS.Admin.Infrastructure.Toasts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Infrastructure.Filtros;

/// <summary>
/// Red de seguridad: si la base de datos rechaza un cambio (por ejemplo, eliminar algo que otros
/// registros usan), se muestra un aviso y se regresa a la página en lugar de la página de error.
/// </summary>
public class FiltroErroresDeDatos : IAsyncPageFilter
{
    private readonly ILogger<FiltroErroresDeDatos> _logger;

    public FiltroErroresDeDatos(ILogger<FiltroErroresDeDatos> logger) => _logger = logger;

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var resultado = await next();
        if (resultado.Exception is not DbUpdateException ex || resultado.ExceptionHandled) return;
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method) || context.HandlerInstance is not PageModel pagina) return;

        _logger.LogWarning(ex, "La base de datos rechazó un cambio en {Ruta}", context.HttpContext.Request.Path);
        var eliminando = context.HandlerMethod?.Name?.Contains("Eliminar", StringComparison.OrdinalIgnoreCase) == true;
        pagina.ToastError(eliminando
            ? "No se puede eliminar porque otros registros lo usan (cobros, trabajos, tickets…). Puedes desactivarlo o cancelarlo en su lugar."
            : "No se pudo guardar porque entra en conflicto con otros registros. Recarga la página e inténtalo de nuevo.");
        resultado.ExceptionHandled = true;
        var referer = context.HttpContext.Request.Headers.Referer.ToString();
        resultado.Result = Uri.TryCreate(referer, UriKind.Absolute, out var u) && u.Host == context.HttpContext.Request.Host.Host
            ? new LocalRedirectResult(u.PathAndQuery)
            : new RedirectResult("/");
    }
}
