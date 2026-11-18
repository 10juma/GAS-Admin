using GAS.Admin.Security;
using Microsoft.Data.SqlClient;

namespace GAS.Admin.Infrastructure.Configuracion;

/// <summary>
/// Fuente de configuración con las llaves capturadas en GAS-Admin (tabla LlavesConfiguracion, cifradas).
/// Se agrega al final, así que lo capturado en pantalla tiene prioridad sobre user-secrets y variables de entorno.
/// Al guardar una llave se llama <see cref="Recargar"/> y las opciones (IOptionsMonitor) se actualizan sin reiniciar.
/// </summary>
public sealed class FuenteConfiguracionBd(string conexion, CifradoConfiguracion cifrado) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => ProveedorConfiguracionBd.Crear(conexion, cifrado);
}

public sealed class ProveedorConfiguracionBd : ConfigurationProvider
{
    private static ProveedorConfiguracionBd? _actual;
    private readonly string _conexion;
    private readonly CifradoConfiguracion _cifrado;

    private ProveedorConfiguracionBd(string conexion, CifradoConfiguracion cifrado)
    {
        _conexion = conexion;
        _cifrado = cifrado;
    }

    internal static ProveedorConfiguracionBd Crear(string conexion, CifradoConfiguracion cifrado) =>
        _actual = new ProveedorConfiguracionBd(conexion, cifrado);

    /// <summary>Vuelve a leer las llaves de la BD y avisa a las opciones que cambiaron.</summary>
    public static void Recargar()
    {
        if (_actual is null) return;
        _actual.Load();
        _actual.OnReload();
    }

    /// <summary>Claves que no se pudieron descifrar (llave maestra distinta a la que las cifró).</summary>
    public static IReadOnlyCollection<string> Ilegibles => _actual?._ilegibles ?? [];
    private List<string> _ilegibles = [];

    public override void Load()
    {
        var datos = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var ilegibles = new List<string>();
        if (_cifrado.Disponible)
        {
            try
            {
                using var cn = new SqlConnection(_conexion);
                cn.Open();
                using var cmd = new SqlCommand("SELECT Clave, ValorCifrado FROM LlavesConfiguracion", cn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var clave = r.GetString(0);
                    var valor = _cifrado.Descifrar(r.GetString(1), clave);
                    if (valor is null) ilegibles.Add(clave);
                    else datos[clave] = valor;
                }
            }
            catch (SqlException)
            {
                // Tabla aún sin crear (migración pendiente) o BD sin conexión: se sigue con user-secrets y entorno.
            }
        }
        Data = datos;
        _ilegibles = ilegibles;
    }
}
