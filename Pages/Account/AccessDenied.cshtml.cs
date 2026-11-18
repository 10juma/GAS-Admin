using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Pages.Account;

public class AccessDeniedModel : PageModel
{
    private readonly IAuditoria _auditoria;

    public AccessDeniedModel(IAuditoria auditoria) => _auditoria = auditoria;

    public async Task OnGetAsync(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            await _auditoria.RegistrarAsync(AccionesAuditoria.AccesoDenegado, detalle: returnUrl);
        }
    }
}
