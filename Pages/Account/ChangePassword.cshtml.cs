using System.ComponentModel.DataAnnotations;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Pages.Account;

public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditoria _auditoria;
    private readonly ILogger<ChangePasswordModel> _logger;

    public ChangePasswordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditoria auditoria,
        ILogger<ChangePasswordModel> logger)
    {
        _auditoria = auditoria;
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public ChangePasswordInput Input { get; set; } = new();

    /// <summary>El usuario tiene contraseña temporal y debe cambiarla para continuar.</summary>
    public bool Obligatorio => User.HasClaim(GasClaims.DebeCambiarPassword, "true");

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!string.IsNullOrEmpty(Input.NewPassword) && Input.NewPassword == Input.CurrentPassword)
        {
            ModelState.AddModelError(string.Empty, "La nueva contraseña debe ser distinta de la actual.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var result = await _userManager.ChangePasswordAsync(
            user, Input.CurrentPassword, Input.NewPassword);

        if (!result.Succeeded)
        {
            IdentityErrores.AgregarA(ModelState, result);
            return Page();
        }

        var eraObligatorio = user.DebeCambiarPassword;
        if (eraObligatorio)
        {
            user.DebeCambiarPassword = false;
            await _userManager.UpdateAsync(user);
        }

        await _auditoria.RegistrarAsync(AccionesAuditoria.PasswordCambiada, usuario: user);

        // Renueva la cookie (el sello de seguridad cambió con la contraseña).
        await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("Usuario cambió su contraseña.");

        this.ToastSuccess("Tu contraseña se actualizó correctamente.");
        return eraObligatorio ? Redirect("/") : RedirectToPage();
    }

    public class ChangePasswordInput
    {
        [Required(ErrorMessage = "Ingresa tu contraseña actual.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa la nueva contraseña.")]
        [StringLength(100, MinimumLength = 10,
            ErrorMessage = "La nueva contraseña debe tener al menos 10 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirma la nueva contraseña.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar nueva contraseña")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
