using System.ComponentModel.DataAnnotations;
using GAS.Admin.Services.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Pages.Account;

public class RecuperarPasswordModel : PageModel
{
    private readonly RecuperacionPassword _recuperacion;

    public RecuperarPasswordModel(RecuperacionPassword recuperacion) => _recuperacion = recuperacion;

    [BindProperty]
    [Required(ErrorMessage = "Ingresa tu correo electrónico.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    [StringLength(256)]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    public bool Enviado { get; private set; }
    public bool Disponible => _recuperacion.Disponible;

    public IActionResult OnGet(string? email)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect("/");
        Email = email ?? "";
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Disponible || !ModelState.IsValid) return Page();

        var ok = await _recuperacion.SolicitarAsync(Email, Request,
            (id, codigo) => Url.Page("/Account/NuevaPassword", new { id, codigo })!);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "Hiciste demasiadas solicitudes. Espera una hora e inténtalo de nuevo.");
            return Page();
        }

        Enviado = true;
        return Page();
    }
}
