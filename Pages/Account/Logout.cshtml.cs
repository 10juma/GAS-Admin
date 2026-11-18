using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditoria _auditoria;

    public LogoutModel(SignInManager<ApplicationUser> signInManager, IAuditoria auditoria)
    {
        _signInManager = signInManager;
        _auditoria = auditoria;
    }

    // Cerrar sesión solo por POST (con token antifalsificación).
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        await _auditoria.RegistrarAsync(AccionesAuditoria.Logout);
        await _signInManager.SignOutAsync();
        this.ToastInfo("Cerraste sesión correctamente.");
        return RedirectToPage("/Account/Login");
    }
}
