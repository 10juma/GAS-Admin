using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    /// <summary>Código HTTP cuando la página se muestra por un estado (404, 403…).</summary>
    [BindProperty(SupportsGet = true)] public int? Codigo { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId) && Codigo is null or >= 500;

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }

    // Las peticiones POST que terminan en error también se re-ejecutan aquí.
    public void OnPost() => OnGet();
}

