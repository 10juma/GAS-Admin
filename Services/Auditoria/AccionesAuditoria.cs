namespace GAS.Admin.Services.Auditoria;

public enum TonoAccion { Neutro, Exito, Advertencia, Peligro }

public enum CategoriaAccion { Accesos, Alertas, Usuarios, Roles, Datos }

public sealed record AccionInfo(string Clave, string Nombre, TonoAccion Tono, CategoriaAccion Categoria);

/// <summary>Acciones que se registran en la bitácora.</summary>
public static class AccionesAuditoria
{
    public const string LoginExitoso = "LoginExitoso";
    public const string LoginFallido = "LoginFallido";
    public const string CuentaBloqueada = "CuentaBloqueada";
    public const string LoginDenegado = "LoginDenegado";
    public const string Logout = "Logout";
    public const string PasswordCambiada = "PasswordCambiada";
    public const string AccesoDenegado = "AccesoDenegado";
    public const string RecuperacionSolicitada = "RecuperacionSolicitada";
    public const string PasswordRecuperada = "PasswordRecuperada";

    public const string UsuarioCreado = "UsuarioCreado";
    public const string UsuarioActualizado = "UsuarioActualizado";
    public const string UsuarioActivado = "UsuarioActivado";
    public const string UsuarioDesactivado = "UsuarioDesactivado";
    public const string UsuarioDesbloqueado = "UsuarioDesbloqueado";
    public const string UsuarioEliminado = "UsuarioEliminado";
    public const string PasswordRestablecida = "PasswordRestablecida";

    public const string RolCreado = "RolCreado";
    public const string RolEliminado = "RolEliminado";
    public const string PermisosActualizados = "PermisosActualizados";

    public const string RegistroCreado = "RegistroCreado";
    public const string RegistroActualizado = "RegistroActualizado";
    public const string RegistroEliminado = "RegistroEliminado";
    public const string CotizacionEnviada = "CotizacionEnviada";
    public const string ArchivoSubido = "ArchivoSubido";
    public const string ArchivoEliminado = "ArchivoEliminado";
    public const string AccionEnProducto = "AccionEnProducto";
    public const string AccesoSuperAdmin = "AccesoSuperAdmin";
    public const string LlaveActualizada = "LlaveActualizada";
    public const string ServidorPreparado = "ServidorPreparado";
    public const string DespliegueIniciado = "DespliegueIniciado";
    public const string DespliegueTerminado = "DespliegueTerminado";
    public const string RespaldoIniciado = "RespaldoIniciado";
    public const string RespaldoTerminado = "RespaldoTerminado";
    public const string RespaldoDescargado = "RespaldoDescargado";

    public static readonly IReadOnlyList<AccionInfo> Catalogo =
    [
        new(LoginExitoso, "Inicio de sesión", TonoAccion.Exito, CategoriaAccion.Accesos),
        new(LoginFallido, "Inicio de sesión fallido", TonoAccion.Advertencia, CategoriaAccion.Alertas),
        new(CuentaBloqueada, "Cuenta bloqueada", TonoAccion.Peligro, CategoriaAccion.Alertas),
        new(LoginDenegado, "Acceso de cuenta inactiva", TonoAccion.Peligro, CategoriaAccion.Alertas),
        new(Logout, "Cierre de sesión", TonoAccion.Neutro, CategoriaAccion.Accesos),
        new(PasswordCambiada, "Cambio de contraseña", TonoAccion.Neutro, CategoriaAccion.Accesos),
        new(AccesoDenegado, "Acceso denegado", TonoAccion.Advertencia, CategoriaAccion.Alertas),
        new(RecuperacionSolicitada, "Recuperación de contraseña solicitada", TonoAccion.Neutro, CategoriaAccion.Accesos),
        new(PasswordRecuperada, "Contraseña recuperada por correo", TonoAccion.Advertencia, CategoriaAccion.Accesos),
        new(UsuarioCreado, "Usuario creado", TonoAccion.Exito, CategoriaAccion.Usuarios),
        new(UsuarioActualizado, "Usuario actualizado", TonoAccion.Neutro, CategoriaAccion.Usuarios),
        new(UsuarioActivado, "Usuario activado", TonoAccion.Exito, CategoriaAccion.Usuarios),
        new(UsuarioDesactivado, "Usuario desactivado", TonoAccion.Advertencia, CategoriaAccion.Usuarios),
        new(UsuarioDesbloqueado, "Usuario desbloqueado", TonoAccion.Neutro, CategoriaAccion.Usuarios),
        new(UsuarioEliminado, "Usuario eliminado", TonoAccion.Peligro, CategoriaAccion.Usuarios),
        new(PasswordRestablecida, "Contraseña restablecida", TonoAccion.Advertencia, CategoriaAccion.Usuarios),
        new(RolCreado, "Rol creado", TonoAccion.Exito, CategoriaAccion.Roles),
        new(RolEliminado, "Rol eliminado", TonoAccion.Peligro, CategoriaAccion.Roles),
        new(PermisosActualizados, "Permisos actualizados", TonoAccion.Neutro, CategoriaAccion.Roles),
        new(RegistroCreado, "Registro creado", TonoAccion.Exito, CategoriaAccion.Datos),
        new(RegistroActualizado, "Registro actualizado", TonoAccion.Neutro, CategoriaAccion.Datos),
        new(RegistroEliminado, "Registro eliminado", TonoAccion.Peligro, CategoriaAccion.Datos),
        new(CotizacionEnviada, "Cotización enviada", TonoAccion.Exito, CategoriaAccion.Datos),
        new(ArchivoSubido, "Archivo subido", TonoAccion.Neutro, CategoriaAccion.Datos),
        new(ArchivoEliminado, "Archivo eliminado", TonoAccion.Advertencia, CategoriaAccion.Datos),
        new(AccionEnProducto, "Acción en producto", TonoAccion.Advertencia, CategoriaAccion.Datos),
        new(AccesoSuperAdmin, "Acceso a super-admin", TonoAccion.Neutro, CategoriaAccion.Accesos),
        new(LlaveActualizada, "Llave de configuración", TonoAccion.Advertencia, CategoriaAccion.Accesos),
        new(ServidorPreparado, "Servidor preparado para despliegues", TonoAccion.Advertencia, CategoriaAccion.Accesos),
        new(DespliegueIniciado, "Despliegue iniciado", TonoAccion.Advertencia, CategoriaAccion.Datos),
        new(DespliegueTerminado, "Despliegue terminado", TonoAccion.Neutro, CategoriaAccion.Datos),
        new(RespaldoIniciado, "Respaldo iniciado", TonoAccion.Neutro, CategoriaAccion.Datos),
        new(RespaldoTerminado, "Respaldo terminado", TonoAccion.Neutro, CategoriaAccion.Datos),
        new(RespaldoDescargado, "Respaldo descargado", TonoAccion.Advertencia, CategoriaAccion.Accesos),
    ];

    public static readonly IReadOnlyList<(CategoriaAccion Categoria, string Nombre)> Categorias =
    [
        (CategoriaAccion.Accesos, "Accesos"),
        (CategoriaAccion.Alertas, "Alertas"),
        (CategoriaAccion.Usuarios, "Usuarios"),
        (CategoriaAccion.Roles, "Roles y permisos"),
        (CategoriaAccion.Datos, "Catálogos"),
    ];

    public static List<string> DeCategoria(CategoriaAccion categoria) =>
        Catalogo.Where(a => a.Categoria == categoria).Select(a => a.Clave).ToList();

    private static readonly Dictionary<string, AccionInfo> PorClave =
        Catalogo.ToDictionary(a => a.Clave);

    public static AccionInfo Info(string clave) =>
        PorClave.TryGetValue(clave, out var info) ? info : new(clave, clave, TonoAccion.Neutro, CategoriaAccion.Accesos);
}
