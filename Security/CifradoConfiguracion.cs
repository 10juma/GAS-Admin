using System.Security.Cryptography;
using System.Text;

namespace GAS.Admin.Security;

/// <summary>
/// Cifrado AES-256-GCM de las llaves guardadas en la BD. La llave maestra (32 bytes en Base64) se toma de la
/// variable de entorno Seguridad__LlaveMaestra o, si no existe, de un archivo local fuera del proyecto
/// (~/.gas-admin/llave-maestra.key). En Development ese archivo se genera solo la primera vez.
/// </summary>
public sealed class CifradoConfiguracion
{
    public const string ClaveConfiguracion = "Seguridad:LlaveMaestra";
    public const string ClaveArchivo = "Seguridad:ArchivoLlaveMaestra";
    private readonly byte[]? _llave;

    /// <summary>De dónde salió la llave (para mostrarlo en Configuración; nunca el valor).</summary>
    public string Origen { get; private init; } = "Sin llave maestra";

    public static string RutaPorDefecto =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gas-admin", "llave-maestra.key");

    /// <summary>Obtiene la llave maestra sin pedir configuración: entorno → archivo local → (solo Development) la genera.</summary>
    public static CifradoConfiguracion Crear(IConfiguration config, IHostEnvironment entorno)
    {
        var deEntorno = config[ClaveConfiguracion];
        if (!string.IsNullOrWhiteSpace(deEntorno))
            return new CifradoConfiguracion(deEntorno) { Origen = "Variable de entorno Seguridad__LlaveMaestra" };

        var ruta = config[ClaveArchivo] is { Length: > 0 } r ? r : RutaPorDefecto;
        if (File.Exists(ruta))
            return new CifradoConfiguracion(File.ReadAllText(ruta)) { Origen = $"Archivo local {ruta}" };

        if (!entorno.IsDevelopment())
            return new CifradoConfiguracion(null) { Origen = $"Falta: define Seguridad__LlaveMaestra o el archivo {ruta}" };

        // Development: se crea una sola vez, legible solo por tu usuario.
        var carpeta = Path.GetDirectoryName(ruta)!;
        Directory.CreateDirectory(carpeta);
        var llave = GenerarLlave();
        File.WriteAllText(ruta, llave);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(carpeta, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(ruta, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return new CifradoConfiguracion(llave) { Origen = $"Archivo local {ruta} (generado automáticamente)" };
    }

    public CifradoConfiguracion(string? llaveBase64)
    {
        if (string.IsNullOrWhiteSpace(llaveBase64)) return;
        try
        {
            var bytes = Convert.FromBase64String(llaveBase64.Trim());
            if (bytes.Length == 32) _llave = bytes;
        }
        catch (FormatException) { }
    }

    public bool Disponible => _llave is not null;

    public static string GenerarLlave() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string Cifrar(string texto, string clave)
    {
        if (_llave is null) throw new InvalidOperationException("Falta la llave maestra (Seguridad:LlaveMaestra).");
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plano = Encoding.UTF8.GetBytes(texto);
        var cifrado = new byte[plano.Length];
        var etiqueta = new byte[16];
        using var aes = new AesGcm(_llave, 16);
        // La ruta de la llave va como dato asociado: un valor copiado a otra fila no se descifra.
        aes.Encrypt(nonce, plano, cifrado, etiqueta, Encoding.UTF8.GetBytes(clave));
        return Convert.ToBase64String([.. nonce, .. etiqueta, .. cifrado]);
    }

    public string? Descifrar(string base64, string clave)
    {
        if (_llave is null) return null;
        try
        {
            var datos = Convert.FromBase64String(base64);
            if (datos.Length < 28) return null;
            var plano = new byte[datos.Length - 28];
            using var aes = new AesGcm(_llave, 16);
            aes.Decrypt(datos.AsSpan(0, 12), datos.AsSpan(28), datos.AsSpan(12, 16), plano, Encoding.UTF8.GetBytes(clave));
            return Encoding.UTF8.GetString(plano);
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
