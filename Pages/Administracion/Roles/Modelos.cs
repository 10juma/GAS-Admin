namespace GAS.Admin.Pages.Administracion.Roles;

/// <summary>Modal de permisos de un rol.</summary>
public class RolModalVm
{
    public string Id { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public int Usuarios { get; init; }
    public bool EsAdmin { get; init; }
    public bool EsBase { get; init; }
    public bool PuedeGestionar { get; init; }
    public HashSet<string> Asignados { get; init; } = [];
    public bool Editable => PuedeGestionar && !EsAdmin;
    public bool PuedeEliminar => PuedeGestionar && !EsBase && Usuarios == 0;
}

/// <summary>Modal de alta de rol.</summary>
public sealed record RolNuevoVm(string Nombre);
