using System.ComponentModel.DataAnnotations;

namespace GAS.Admin.Pages.Administracion.Usuarios;

public class UsuarioInput
{
    [Required(ErrorMessage = "Ingresa el nombre completo.")]
    [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres.")]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el correo electrónico.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    [StringLength(256)]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}

/// <summary>Datos del modal de alta (Id nulo) o edición de un usuario.</summary>
public class UsuarioModalVm
{
    public string? Id { get; init; }
    public bool EsNuevo => Id is null;
    public UsuarioInput Input { get; init; } = new();
    public List<RolOpcion> RolesDisponibles { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public bool EsUsuarioActual { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool Activo { get; init; } = true;
    public bool Bloqueado { get; init; }
    public bool DebeCambiarPassword { get; init; }
    public DateTime? FechaAltaUtc { get; init; }
    public DateTime? UltimoAccesoUtc { get; init; }
    public DateTime? BloqueadoHastaUtc { get; init; }

    /// <summary>Filtros de la lista para regresar a la misma vista.</summary>
    public string? Estado { get; init; }
    public string? Q { get; init; }
}

public sealed record PasswordTemporalVm(string Nombre, string Email, string Password, bool EsNuevo);
