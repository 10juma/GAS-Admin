namespace GAS.Admin.Security;

public sealed record PermisoDef(string Valor, string Nombre);

public sealed record ModuloPermisos(string Clave, string Nombre, string Descripcion, IReadOnlyList<PermisoDef> Permisos);

/// <summary>
/// Catálogo de permisos por módulo. Se asignan a los roles como claims
/// ("permiso") y se validan con políticas del mismo nombre.
/// </summary>
public static class Permisos
{
    public const string ClaimType = "permiso";

    public static class Dashboard { public const string Ver = "Dashboard.Ver"; }
    public static class Organizacion { public const string Ver = "Organizacion.Ver"; public const string Gestionar = "Organizacion.Gestionar"; }
    public static class Comercial { public const string Ver = "Comercial.Ver"; public const string Gestionar = "Comercial.Gestionar"; }
    public static class Productos { public const string Ver = "Productos.Ver"; public const string Gestionar = "Productos.Gestionar"; }
    public static class Finanzas { public const string Ver = "Finanzas.Ver"; public const string Gestionar = "Finanzas.Gestionar"; }
    public static class Proyectos { public const string Ver = "Proyectos.Ver"; public const string Gestionar = "Proyectos.Gestionar"; }
    public static class Infraestructura { public const string Ver = "Infraestructura.Ver"; public const string Gestionar = "Infraestructura.Gestionar"; }
    public static class Soporte { public const string Ver = "Soporte.Ver"; public const string Gestionar = "Soporte.Gestionar"; }
    public static class Direccion { public const string Ver = "Direccion.Ver"; }
    public static class Usuarios { public const string Ver = "Usuarios.Ver"; public const string Gestionar = "Usuarios.Gestionar"; }
    public static class Roles { public const string Ver = "Roles.Ver"; public const string Gestionar = "Roles.Gestionar"; }
    public static class Auditoria { public const string Ver = "Auditoria.Ver"; }

    private static PermisoDef VerP(string v) => new(v, "Ver");
    private static PermisoDef GestionarP(string v) => new(v, "Gestionar");

    public static readonly IReadOnlyList<ModuloPermisos> Modulos =
    [
        new("Dashboard", "Dashboard", "Resumen general de Global App Suite", [VerP(Dashboard.Ver)]),
        new("Organizacion", "Organización", "Empresa, departamentos, puestos y personal", [VerP(Organizacion.Ver), GestionarP(Organizacion.Gestionar)]),
        new("Comercial", "Comercial", "Prospectos, clientes, propuestas y cotizaciones", [VerP(Comercial.Ver), GestionarP(Comercial.Gestionar)]),
        new("Productos", "Productos y servicios", "Catálogo comercial y suscripciones", [VerP(Productos.Ver), GestionarP(Productos.Gestionar)]),
        new("Finanzas", "Finanzas", "Ingresos, gastos, costos y rentabilidad", [VerP(Finanzas.Ver), GestionarP(Finanzas.Gestionar)]),
        new("Proyectos", "Proyectos y operaciones", "Proyectos, tareas, Kanban y entregables", [VerP(Proyectos.Ver), GestionarP(Proyectos.Gestionar)]),
        new("Infraestructura", "Infraestructura y tecnología", "Proveedores, dominios, repositorios y ambientes", [VerP(Infraestructura.Ver), GestionarP(Infraestructura.Gestionar)]),
        new("Soporte", "Atención al cliente", "Tickets, solicitudes y seguimiento", [VerP(Soporte.Ver), GestionarP(Soporte.Gestionar)]),
        new("Direccion", "Dirección", "Indicadores y reportes empresariales", [VerP(Direccion.Ver)]),
        new("Usuarios", "Usuarios", "Usuarios internos de GAS-Admin", [VerP(Usuarios.Ver), GestionarP(Usuarios.Gestionar)]),
        new("Roles", "Roles y permisos", "Roles y permisos por módulo", [VerP(Roles.Ver), GestionarP(Roles.Gestionar)]),
        new("Auditoria", "Auditoría", "Bitácora de acciones del sistema", [VerP(Auditoria.Ver)]),
    ];

    public static readonly IReadOnlySet<string> Todos =
        Modulos.SelectMany(m => m.Permisos).Select(p => p.Valor).ToHashSet();
}
