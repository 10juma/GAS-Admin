using GAS.Admin.Models.Entities;
using Microsoft.Data.SqlClient;

namespace GAS.Admin.Services.Despliegues;

/// <summary>Base de datos de un ambiente: en qué contenedor vive, cómo se llama y con qué usuario se respalda.</summary>
public sealed record OrigenBaseDatos(string Contenedor, string BaseDatos, string Usuario, string Contrasena)
{
    /// <summary>Contenedor y base juntos, para no respaldar dos veces la misma base (web y API comparten).</summary>
    public string Llave => $"{Contenedor}/{BaseDatos}".ToLowerInvariant();

    /// <summary>
    /// Lee la cadena de conexión de las variables del ambiente: DefaultConnection o Default (cada producto la
    /// nombra distinto) o, si no, la única ConnectionStrings__* que haya. Devuelve el error en texto si falta algo.
    /// </summary>
    public static (OrigenBaseDatos? Origen, string? Error) Resolver(ConfiguracionDespliegue c, IReadOnlyDictionary<string, string> vars)
    {
        var clave = new[] { "ConnectionStrings__DefaultConnection", "ConnectionStrings__Default" }.FirstOrDefault(vars.ContainsKey)
                    ?? (vars.Keys.Where(k => k.StartsWith("ConnectionStrings__", StringComparison.Ordinal)).ToList() is [var unica] ? unica : null);
        if (clave is null)
            return (null, "Para respaldar se necesita la variable ConnectionStrings__DefaultConnection (o ConnectionStrings__Default) en el ambiente.");
        SqlConnectionStringBuilder csb;
        try { csb = new SqlConnectionStringBuilder(vars[clave]); }
        catch (ArgumentException) { return (null, $"{clave} no es una cadena de conexión válida."); }

        var contenedor = string.IsNullOrWhiteSpace(c.ContenedorSql) ? csb.DataSource.Split(',')[0].Split('\\')[0] : c.ContenedorSql.Trim();
        var bd = string.IsNullOrWhiteSpace(c.BaseDatos) ? csb.InitialCatalog : c.BaseDatos.Trim();
        if (string.IsNullOrWhiteSpace(bd)) return (null, "No se sabe qué base respaldar: captura la base de datos en la configuración.");
        if (string.IsNullOrWhiteSpace(contenedor)) return (null, "No se sabe en qué contenedor está SQL Server: captúralo en la configuración.");
        return (new OrigenBaseDatos(contenedor, bd, csb.UserID, csb.Password), null);
    }

    /// <summary>
    /// Comando para el servidor que respalda la base en /var/opt/mssql/backup/{archivo} dentro del contenedor.
    /// La contraseña se toma de {archivoEnv} (subido con permisos 600), que el mismo comando borra al leerlo.
    /// </summary>
    public string ComandoRespaldo(string archivoEnv, string archivo, bool conChecksum)
    {
        var sql = $"BACKUP DATABASE [{BaseDatos.Replace("]", "]]")}] TO DISK = N'/var/opt/mssql/backup/{archivo}' WITH COPY_ONLY, INIT" +
                  (conChecksum ? ", CHECKSUM" : "");
        var q = Sh.Q(Contenedor);
        return $"set -a; . {archivoEnv}; set +a; rm -f {archivoEnv}; " +
               $"P=/opt/mssql-tools18/bin/sqlcmd; C=-C; docker exec {q} test -x $P || {{ P=/opt/mssql-tools/bin/sqlcmd; C=; }}; " +
               $"docker exec {q} mkdir -p /var/opt/mssql/backup && " +
               $"docker exec -e SQLCMDPASSWORD {q} $P -S localhost -U {Sh.Q(Usuario)} $C -b -Q {Sh.Q(sql)}";
    }

    public string ArchivoEnv => $"SQLCMDPASSWORD={Sh.Q(Contrasena)}\n";
}
