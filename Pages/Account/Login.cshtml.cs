using System.ComponentModel.DataAnnotations;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditoria _auditoria;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditoria auditoria,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditoria = auditoria;
        _logger = logger;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl());
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim();
        var result = await _signInManager.PasswordSignInAsync(
            email,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        var usuario = await _userManager.FindByEmailAsync(email);

        if (result.Succeeded && usuario is not null)
        {
            usuario.UltimoAccesoUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(usuario);
            await _auditoria.RegistrarAsync(AccionesAuditoria.LoginExitoso, usuario: usuario);
            _logger.LogInformation("Inicio de sesión correcto.");

            if (usuario.DebeCambiarPassword)
            {
                this.ToastInfo("Por seguridad, define una contraseña propia para continuar.");
                return RedirectToPage("/Account/ChangePassword");
            }

            return LocalRedirect(SafeReturnUrl());
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Cuenta bloqueada por intentos fallidos.");
            await _auditoria.RegistrarAsync(AccionesAuditoria.CuentaBloqueada, usuario: usuario, email: email);
            this.ToastWarning(
                "Tu cuenta está bloqueada temporalmente por varios intentos fallidos. Inténtalo en 15 minutos.");
            return Page();
        }

        // Identity revisa si la cuenta puede entrar antes que la contraseña. Solo
        // revelamos que está desactivada si la contraseña es correcta.
        if (result.IsNotAllowed && usuario is not null
            && await _userManager.CheckPasswordAsync(usuario, Input.Password))
        {
            await _auditoria.RegistrarAsync(AccionesAuditoria.LoginDenegado, usuario: usuario, email: email);
            this.ToastWarning("Tu cuenta está desactivada. Contacta a un administrador.");
            return Page();
        }

        await _auditoria.RegistrarAsync(
            AccionesAuditoria.LoginFallido,
            detalle: usuario is null ? "Correo no registrado" : "Contraseña incorrecta",
            usuario: usuario, email: email);
        this.ToastError("Correo o contraseña incorrectos.");
        return Page();
    }

    private string SafeReturnUrl() =>
        !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl
            : "/";

    public class LoginInput
    {
        [Required(ErrorMessage = "Ingresa tu correo electrónico.")]
        [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu contraseña.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }
}
