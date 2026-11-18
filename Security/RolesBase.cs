namespace GAS.Admin.Security;

/// <summary>
/// Roles iniciales de GAS-Admin y sus permisos por defecto.
/// Los permisos solo se siembran al crear el rol; después se administran desde la pantalla de roles.
/// </summary>
public static class RolesBase
{
    /// <summary>Acceso total; no usa la lista de permisos.</summary>
    public const string Admin = "Admin";
    public const string Desarrollador = "Desarrollador";
    public const string Finanzas = "Finanzas";
    public const string Soporte = "Soporte";

    public static readonly IReadOnlyDictionary<string, string[]> PermisosPorDefecto =
        new Dictionary<string, string[]>
        {
            [Admin] = [],
            [Desarrollador] =
            [
                Permisos.Dashboard.Ver,
                Permisos.Productos.Ver,
                Permisos.Proyectos.Ver, Permisos.Proyectos.Gestionar,
                Permisos.Infraestructura.Ver, Permisos.Infraestructura.Gestionar,
                Permisos.Soporte.Ver,
            ],
            [Finanzas] =
            [
                Permisos.Dashboard.Ver,
                Permisos.Comercial.Ver,
                Permisos.Productos.Ver,
                Permisos.Finanzas.Ver, Permisos.Finanzas.Gestionar,
                Permisos.Proyectos.Ver,
                Permisos.Direccion.Ver,
            ],
            [Soporte] =
            [
                Permisos.Dashboard.Ver,
                Permisos.Comercial.Ver,
                Permisos.Productos.Ver,
                Permisos.Proyectos.Ver,
                Permisos.Soporte.Ver, Permisos.Soporte.Gestionar,
            ],
        };

    public static bool EsBase(string? rol) => rol is not null && PermisosPorDefecto.ContainsKey(rol);
}
