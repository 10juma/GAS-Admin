using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using ConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace GAS.Admin.Services.Despliegues;

public class ErrorSsh(string mensaje, Exception? interna = null) : Exception(mensaje, interna);

/// <summary>Resultado de un comando remoto: código de salida y salida completa (stdout + stderr).</summary>
public sealed record ResultadoComando(int Codigo, string Salida)
{
    public bool Ok => Codigo == 0;
}

/// <summary>
/// Conexión SSH/SFTP con un servidor. Verifica la huella del servidor en cada conexión:
/// si no coincide con la registrada al prepararlo, no se conecta (evita hablar con un impostor).
/// </summary>
public sealed class ClienteSsh : IDisposable
{
    private readonly SshClient _ssh;
    private readonly SftpClient _sftp;
    private string? _huellaRecibida;
    private bool _huellaRechazada;

    /// <summary>Huella SHA-256 que presentó el servidor (formato "SHA256:…", como ssh-keygen -l).</summary>
    public string? HuellaRecibida => _huellaRecibida;

    private ClienteSsh(ConnectionInfo info, string? huellaEsperada)
    {
        _ssh  = new SshClient(info);
        _sftp = new SftpClient(info);
        void Verificar(object? _, HostKeyEventArgs e)
        {
            _huellaRecibida = "SHA256:" + e.FingerPrintSHA256;
            e.CanTrust = huellaEsperada is null || string.Equals(huellaEsperada, _huellaRecibida, StringComparison.Ordinal);
            _huellaRechazada = !e.CanTrust;
        }
        _ssh.HostKeyReceived  += Verificar;
        _sftp.HostKeyReceived += Verificar;
        _ssh.KeepAliveInterval = TimeSpan.FromSeconds(30);
    }

    /// <summary>Conexión con usuario y contraseña (solo para preparar el servidor; la contraseña no se guarda).</summary>
    public static ClienteSsh ConContrasena(string host, int puerto, string usuario, string contrasena, string? huellaEsperada)
    {
        var teclado = new KeyboardInteractiveAuthenticationMethod(usuario);
        teclado.AuthenticationPrompt += (_, e) => { foreach (var p in e.Prompts) p.Response = contrasena; };
        var info = new ConnectionInfo(host, puerto, usuario, new PasswordAuthenticationMethod(usuario, contrasena), teclado)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        return new ClienteSsh(info, huellaEsperada);
    }

    /// <summary>Conexión con la llave privada de GAS-Admin; exige la huella registrada.</summary>
    public static ClienteSsh ConLlave(string host, int puerto, string usuario, string llavePrivadaPem, string huellaEsperada)
    {
        var llave = new PrivateKeyFile(new MemoryStream(Encoding.ASCII.GetBytes(llavePrivadaPem)));
        var info = new ConnectionInfo(host, puerto, usuario, new PrivateKeyAuthenticationMethod(usuario, llave))
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        return new ClienteSsh(info, huellaEsperada);
    }

    public async Task ConectarAsync(CancellationToken ct)
    {
        try
        {
            await _ssh.ConnectAsync(ct);
        }
        catch (SshConnectionException ex) when (_huellaRechazada)
        {
            throw new ErrorSsh($"La huella del servidor cambió ({_huellaRecibida}). Por seguridad no se conectó: " +
                               "si reinstalaste el servidor, vuelve a prepararlo.", ex);
        }
        catch (SshAuthenticationException ex)
        {
            throw new ErrorSsh("El servidor rechazó el acceso (usuario, contraseña o llave incorrectos).", ex);
        }
        catch (Exception ex) when (ex is SshException or System.Net.Sockets.SocketException or SshOperationTimeoutException or TimeoutException)
        {
            throw new ErrorSsh($"No se pudo conectar con el servidor: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ejecuta un comando (con bash, stderr unido a stdout) y entrega cada línea de salida conforme llega.
    /// </summary>
    public async Task<ResultadoComando> EjecutarAsync(string comando, Action<string>? alLinea, TimeSpan limite, CancellationToken ct)
    {
        var envuelto = "bash -lc " + Sh.Q("{ " + comando + "\n} 2>&1");
        using var cmd = _ssh.CreateCommand(envuelto);
        cmd.CommandTimeout = limite;
        var salida = new StringBuilder();

        var ejecucion = cmd.ExecuteAsync(ct);
        using (var lector = new StreamReader(cmd.OutputStream, Encoding.UTF8))
        {
            while (await lector.ReadLineAsync(ct) is { } linea)
            {
                salida.AppendLine(linea);
                alLinea?.Invoke(linea);
            }
        }
        try
        {
            await ejecucion;
        }
        catch (SshOperationTimeoutException)
        {
            throw new ErrorSsh(limite.TotalMinutes >= 1
                ? $"El comando tardó más de {limite.TotalMinutes:0} minutos y se canceló."
                : $"El comando tardó más de {limite.TotalSeconds:0} segundos y se canceló.");
        }
        return new ResultadoComando(cmd.ExitStatus ?? -1, salida.ToString());
    }

    /// <summary>Sube un archivo con los permisos indicados, escritos como en chmod (600 = solo el dueño lee y escribe).</summary>
    public async Task SubirAsync(Stream contenido, string ruta, short permisos, CancellationToken ct)
    {
        if (!_sftp.IsConnected) await _sftp.ConnectAsync(ct);
        await _sftp.UploadFileAsync(contenido, ruta, ct);
        _sftp.ChangePermissions(ruta, permisos);
    }

    public void Dispose()
    {
        if (_sftp.IsConnected) _sftp.Disconnect();
        if (_ssh.IsConnected) _ssh.Disconnect();
        _sftp.Dispose();
        _ssh.Dispose();
    }

    // ── Llaves ────────────────────────────────────────────────────────────────

    /// <summary>Genera un par RSA 4096: llave privada en PEM (PKCS#1) y pública en formato authorized_keys.</summary>
    public static (string PrivadaPem, string Publica) GenerarLlave(string comentario)
    {
        using var rsa = RSA.Create(4096);
        var p = rsa.ExportParameters(false);
        using var ms = new MemoryStream();
        void Escribir(byte[] datos)
        {
            var largo = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(largo, datos.Length);
            ms.Write(largo);
            ms.Write(datos);
        }
        static byte[] Mpint(byte[] v) => v.Length > 0 && (v[0] & 0x80) != 0 ? [0, .. v] : v;
        Escribir(Encoding.ASCII.GetBytes("ssh-rsa"));
        Escribir(Mpint(p.Exponent!));
        Escribir(Mpint(p.Modulus!));
        return (rsa.ExportRSAPrivateKeyPem(), $"ssh-rsa {Convert.ToBase64String(ms.ToArray())} {comentario}");
    }
}

/// <summary>Ayudas para armar comandos de shell sin inyecciones.</summary>
public static class Sh
{
    /// <summary>Comillas simples de bash: todo el texto queda literal.</summary>
    public static string Q(string valor) => "'" + valor.Replace("'", "'\"'\"'") + "'";
}
