using System.ComponentModel.DataAnnotations;
using System.Text;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace GAS.Admin.Pages.Account;

/// <summary>Destino del enlace de "¿Olvidaste tu contraseña?": define una contraseña nueva con el código del correo.</summary>
public class NuevaPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly IAuditoria _auditoria;

    public NuevaPasswordModel(UserManager<ApplicationUser> usuarios, IAuditoria auditoria)
    {
        _usuarios = usuarios;
        _auditoria = auditoria;
    }

    [BindProperty(SupportsGet = true)] public string? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Codigo { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Escribe la nueva contraseña.")]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "La contraseña debe tener al menos 10 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Confirma la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string Confirmacion { get; set; } = string.Empty;

    public bool EnlaceInvalido { get; private set; }
    public string? Email { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect("/");
        var (usuario, token) = await LeerEnlaceAsync();
        // Revisa el código sin consumirlo para avisar desde el principio si ya venció.
        EnlaceInvalido = usuario is null || !await _usuarios.VerifyUserTokenAsync(usuario, _usuarios.Options.Tokens.PasswordResetTokenProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose, token!);
        Email = EnlaceInvalido ? null : usuario!.Email;
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var (usuario, token) = await LeerEnlaceAsync();
        if (usuario is null)
        {
            EnlaceInvalido = true;
            return Page();
        }
        Email = usuario.Email;
        if (!ModelState.IsValid) return Page();

        var resultado = await _usuarios.ResetPasswordAsync(usuario, token!, Password);
        if (!resultado.Succeeded)
        {
            if (resultado.Errors.Any(e => e.Code == "InvalidToken"))
            {
                EnlaceInvalido = true;
                return Page();
            }
            IdentityErrores.AgregarA(ModelState, resultado);
            return Page();
        }

        // ResetPassword cambia el sello de seguridad: el enlace ya no sirve y las sesiones abiertas se cierran.
        usuario.DebeCambiarPassword = false;
        await _usuarios.UpdateAsync(usuario);
        await _usuarios.SetLockoutEndDateAsync(usuario, null);
        await _usuarios.ResetAccessFailedCountAsync(usuario);
        await _auditoria.RegistrarAsync(AccionesAuditoria.PasswordRecuperada, usuario: usuario);

        this.ToastSuccess("Listo, tu contraseña quedó actualizada. Ya puedes iniciar sesión.");
        return RedirectToPage("/Account/Login");
    }

    private async Task<(ApplicationUser? Usuario, string? Token)> LeerEnlaceAsync()
    {
        if (string.IsNullOrEmpty(Id) || string.IsNullOrEmpty(Codigo) || Codigo.Length > 2000) return (null, null);
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Codigo)); }
        catch (FormatException) { return (null, null); }
        var usuario = await _usuarios.FindByIdAsync(Id);
        return usuario is { Activo: true } ? (usuario, token) : (null, null);
    }
}
