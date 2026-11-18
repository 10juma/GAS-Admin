using GAS.Admin.Security;

namespace GAS.Admin.Services.Navegacion;

/// <summary>Opción del menú. <paramref name="Prefijos"/>: páginas que la marcan como activa.</summary>
public sealed record ItemMenu(string Nombre, string Pagina, string? Permiso, string Icono, params string[] Prefijos);

/// <summary>Sección del menú. Con <paramref name="Directo"/> sus opciones se muestran sueltas, sin acordeón.</summary>
public sealed record GrupoMenu(string Clave, string Titulo, string Icono, IReadOnlyList<ItemMenu> Items, bool Directo = false);

/// <summary>Única definición del menú lateral; también da el ícono del título de cada página.</summary>
public static class MenuPrincipal
{
    private const string IcoUsuarios = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75'/>";
    private const string IcoEnchufe = "<path d='M12 22v-5M9 8V2M15 8V2M18 8v5a6 6 0 0 1-12 0V8Z'/>";
    private const string IcoCapas = "<path d='m12 2 10 5-10 5L2 7Z'/><path d='m2 17 10 5 10-5M2 12l10 5 10-5'/>";
    private const string IcoKanban = "<rect x='3' y='3' width='18' height='18' rx='2'/><path d='M8 7v7M12 7v4M16 7v9'/>";
    private const string IcoEdificio = "<rect x='4' y='2' width='16' height='20' rx='2'/><path d='M9 22v-4h6v4M8 6h.01M12 6h.01M16 6h.01M8 10h.01M12 10h.01M16 10h.01M8 14h.01M12 14h.01M16 14h.01'/>";

    public static readonly IReadOnlyList<GrupoMenu> Grupos =
    [
        new("general", "General", "", [
            new("Dashboard", "/Dashboard/Index", null,
                "<rect x='3' y='3' width='7' height='9' rx='1'/><rect x='14' y='3' width='7' height='5' rx='1'/><rect x='14' y='12' width='7' height='9' rx='1'/><rect x='3' y='16' width='7' height='5' rx='1'/>",
                "/Dashboard"),
            new("Dirección", "/Direccion/Index", Permisos.Direccion.Ver,
                "<path d='M22 7 13.5 15.5 8.5 10.5 2 17'/><path d='M16 7h6v6'/>", "/Direccion"),
        ], Directo: true),

        new("productos", "Productos", "<path d='M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z'/><path d='m3.3 7 8.7 5 8.7-5M12 22V12'/>", [
            new("Catálogo", "/Productos/Index", Permisos.Productos.Ver,
                "<rect x='3' y='3' width='7' height='7' rx='1'/><rect x='14' y='3' width='7' height='7' rx='1'/><rect x='14' y='14' width='7' height='7' rx='1'/><rect x='3' y='14' width='7' height='7' rx='1'/>",
                "/Productos/Index"),
            new("Conexiones", "/Productos/Conexiones/Index", Permisos.Productos.Ver, IcoEnchufe, "/Productos/Conexiones"),
            new("Cuentas", "/Productos/Cuentas/Index", Permisos.Productos.Ver, IcoUsuarios, "/Productos/Cuentas"),
        ]),

        new("comercial", "Comercial", "<rect x='2' y='7' width='20' height='14' rx='2'/><path d='M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16'/>", [
            new("Clientes", "/Clientes/Index", Permisos.Comercial.Ver,
                "<rect x='3' y='4' width='18' height='18' rx='2'/><path d='M16 2v4M8 2v4'/><circle cx='12' cy='11' r='3'/><path d='M7 21v-1a2 2 0 0 1 2-2h6a2 2 0 0 1 2 2v1'/>",
                "/Clientes"),
            new("Oportunidades", "/Comercial/Oportunidades/Index", Permisos.Comercial.Ver, IcoKanban, "/Comercial/Oportunidades"),
            new("Cotizaciones", "/Comercial/Cotizaciones/Index", Permisos.Comercial.Ver,
                "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z'/><path d='M14 2v6h6M9 13h6M9 17h3'/>", "/Comercial/Cotizaciones"),
            new("Catálogo comercial", "/Comercial/Catalogo/Index", Permisos.Comercial.Ver, IcoCapas, "/Comercial/Catalogo"),
        ]),

        new("proyectos", "Proyectos", "<path d='M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z'/><path d='M8 10v4M12 10v2M16 10v6'/>", [
            new("Trabajos", "/Proyectos/Index", Permisos.Proyectos.Ver, IcoCapas, "/Proyectos/Index", "/Proyectos/Detalle"),
            new("Tablero de tareas", "/Proyectos/Tareas/Index", Permisos.Proyectos.Ver, IcoKanban, "/Proyectos/Tareas"),
            new("Registro de horas", "/Proyectos/Horas/Index", Permisos.Proyectos.Ver,
                "<circle cx='12' cy='12' r='10'/><path d='M12 6v6l4 2'/>", "/Proyectos/Horas"),
        ]),

        new("finanzas", "Finanzas", "<path d='M3 22h18M6 18v-7M10 18v-7M14 18v-7M18 18v-7M12 2l8 5H4Z'/>", [
            new("Resumen", "/Finanzas/Index", Permisos.Finanzas.Ver,
                "<path d='M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6'/>", "/Finanzas/Index"),
            new("Suscripciones SaaS", "/Finanzas/Suscripciones/Index", Permisos.Finanzas.Ver,
                "<path d='M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8M21 3v5h-5M3 21v-5h5'/>",
                "/Finanzas/Suscripciones"),
            new("Movimientos", "/Finanzas/Movimientos/Index", Permisos.Finanzas.Ver,
                "<path d='m16 3 4 4-4 4M20 7H4M8 21l-4-4 4-4M4 17h16'/>", "/Finanzas/Movimientos"),
            new("Configuración", "/Finanzas/Configuracion/Index", Permisos.Finanzas.Ver,
                "<path d='M19 7V5a2 2 0 0 0-2-2H5a2 2 0 0 0 0 4h14a2 2 0 0 1 2 2v3M3 5v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-3'/><path d='M17 12h4v4h-4a2 2 0 0 1 0-4Z'/>",
                "/Finanzas/Configuracion"),
        ]),

        new("infraestructura", "Infraestructura", "<rect x='4' y='4' width='16' height='16' rx='2'/><rect x='9' y='9' width='6' height='6'/><path d='M15 2v2M15 20v2M2 15h2M2 9h2M20 15h2M20 9h2M9 2v2M9 20v2'/>", [
            new("Panel", "/Infraestructura/Index", Permisos.Infraestructura.Ver, "<path d='M22 12h-4l-3 9L9 3l-3 9H2'/>", "/Infraestructura/Index"),
            new("Ambientes", "/Infraestructura/Ambientes/Index", Permisos.Infraestructura.Ver,
                "<rect x='2' y='3' width='20' height='7' rx='2'/><rect x='2' y='14' width='20' height='7' rx='2'/><path d='M6 6.5h.01M6 17.5h.01'/>",
                "/Infraestructura/Ambientes"),
            new("Repositorios", "/Infraestructura/Repositorios/Index", Permisos.Infraestructura.Ver,
                "<circle cx='6' cy='6' r='3'/><circle cx='6' cy='18' r='3'/><circle cx='18' cy='8' r='3'/><path d='M6 9v6M18 11a6 6 0 0 1-6 6H9'/>",
                "/Infraestructura/Repositorios"),
            new("Dominios", "/Infraestructura/Dominios/Index", Permisos.Infraestructura.Ver,
                "<circle cx='12' cy='12' r='10'/><path d='M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10Z'/>",
                "/Infraestructura/Dominios"),
            new("Servicios externos", "/ServiciosExternos/Index", Permisos.Infraestructura.Ver,
                "<path d='M17.5 19H9a7 7 0 1 1 6.71-9h1.79a4.5 4.5 0 1 1 0 9Z'/>", "/ServiciosExternos"),
            new("Incidentes", "/Infraestructura/Incidentes/Index", Permisos.Infraestructura.Ver,
                "<path d='M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z'/><path d='M12 9v4M12 17h.01'/>",
                "/Infraestructura/Incidentes"),
            new("Credenciales", "/Infraestructura/Credenciales/Index", Permisos.Infraestructura.Ver,
                "<circle cx='7.5' cy='15.5' r='5.5'/><path d='m21 2-9.6 9.6M15.5 7.5l3 3L22 7l-3-3'/>", "/Infraestructura/Credenciales"),
            new("Errores", "/Infraestructura/Errores/Index", Permisos.Infraestructura.Ver,
                "<path d='m8 2 1.88 1.88M14.12 3.88 16 2M9 7.13v-1a3.003 3.003 0 1 1 6 0v1'/><path d='M12 20c-3.3 0-6-2.7-6-6v-3a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v3c0 3.3-2.7 6-6 6M12 20v-9M6.53 9C4.6 8.8 3 7.1 3 5M6 13H2M3 21c0-2.1 1.7-3.9 3.8-4M20.97 5c0 2.1-1.6 3.8-3.5 4M22 13h-4M17.2 17c2.1.1 3.8 1.9 3.8 4'/>",
                "/Infraestructura/Errores"),
            new("Integraciones", "/Infraestructura/Integraciones/Index", Permisos.Infraestructura.Ver, IcoEnchufe, "/Infraestructura/Integraciones"),
        ]),

        new("soporte", "Atención al cliente", "<path d='M7.9 20A9 9 0 1 0 4 16.1L2 22Z'/>", [
            new("Tickets", "/Soporte/Index", Permisos.Soporte.Ver,
                "<path d='M3 14h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a9 9 0 0 1 18 0v7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3'/>",
                "/Soporte/Index", "/Soporte/Detalle"),
            new("Base de conocimientos", "/Soporte/Conocimiento/Index", Permisos.Soporte.Ver,
                "<path d='M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2ZM22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7Z'/>", "/Soporte/Conocimiento"),
        ]),

        new("organizacion", "Organización", IcoEdificio, [
            new("Equipo", "/Organizacion/Index", Permisos.Organizacion.Ver, IcoUsuarios, "/Organizacion/Index"),
            new("Departamentos y puestos", "/Organizacion/Departamentos/Index", Permisos.Organizacion.Ver,
                "<circle cx='12' cy='5' r='3'/><path d='M12 8v4M5 16v-2a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v2'/><circle cx='5' cy='19' r='2'/><circle cx='19' cy='19' r='2'/><circle cx='12' cy='19' r='2'/><path d='M12 12v5'/>",
                "/Organizacion/Departamentos"),
            new("Datos de la empresa", "/Organizacion/Empresa/Index", Permisos.Organizacion.Ver, IcoEdificio, "/Organizacion/Empresa"),
            new("Configuración", "/Organizacion/Configuracion/Index", SoloAdmin,
                "<path d='M20 7h-9M14 17H5'/><circle cx='17' cy='17' r='3'/><circle cx='7' cy='7' r='3'/>", "/Organizacion/Configuracion"),
        ]),

        new("administracion", "Administración", "<path d='M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2Z'/><circle cx='12' cy='12' r='3'/>", [
            new("Usuarios", "/Administracion/Usuarios/Index", Permisos.Usuarios.Ver, IcoUsuarios, "/Administracion/Usuarios"),
            new("Roles y permisos", "/Administracion/Roles/Index", Permisos.Roles.Ver,
                "<path d='M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z'/><path d='m9 12 2 2 4-4'/>", "/Administracion/Roles"),
            new("Auditoría", "/Administracion/Auditoria/Index", Permisos.Auditoria.Ver,
                "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z'/><path d='M14 2v6h6M16 13H8M16 17H8M10 9H8'/>", "/Administracion/Auditoria"),
        ]),
    ];

    /// <summary>Valor de <see cref="ItemMenu.Permiso"/> para opciones que solo ve el rol Admin.</summary>
    public const string SoloAdmin = "rol:" + RolesBase.Admin;

    public static bool PuedeVer(ItemMenu item, System.Security.Claims.ClaimsPrincipal usuario) =>
        item.Permiso is null
        || (item.Permiso.StartsWith("rol:") ? usuario.IsInRole(item.Permiso[4..]) : usuario.TienePermiso(item.Permiso));

    public static bool EsActivo(ItemMenu item, string pagina) =>
        item.Prefijos.Any(p => pagina.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Opción del menú a la que pertenece la página (para el ícono del título).</summary>
    public static ItemMenu? ItemDe(string pagina) =>
        Grupos.SelectMany(g => g.Items).FirstOrDefault(i => EsActivo(i, pagina));

    public static string Svg(string trazos, string? clase = null) =>
        $"<svg{(clase is null ? "" : $" class='{clase}'")} viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>{trazos}</svg>";
}
