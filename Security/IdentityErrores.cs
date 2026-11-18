using Microsoft.AspNetCore.Identity;

namespace GAS.Admin.Security;

/// <summary>Mensajes de Identity en español.</summary>
public static class IdentityErrores
{
    public static string Traducir(IdentityError error) => error.Code switch
    {
        "PasswordMismatch" => "La contraseña actual es incorrecta.",
        "PasswordTooShort" => "La contraseña debe tener al menos 10 caracteres.",
        "PasswordRequiresDigit" => "La contraseña debe incluir al menos un número.",
        "PasswordRequiresUpper" => "La contraseña debe incluir al menos una mayúscula.",
        "PasswordRequiresLower" => "La contraseña debe incluir al menos una minúscula.",
        "DuplicateEmail" or "DuplicateUserName" => "Ya existe un usuario con ese correo electrónico.",
        "InvalidEmail" or "InvalidUserName" => "El correo electrónico no es válido.",
        "DuplicateRoleName" => "Ya existe un rol con ese nombre.",
        "InvalidRoleName" => "El nombre del rol no es válido.",
        "ConcurrencyFailure" => "Otro usuario modificó este registro. Recarga la página e inténtalo de nuevo.",
        _ => error.Description
    };

    public static void AgregarA(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState, IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(string.Empty, Traducir(error));
        }
    }
}
