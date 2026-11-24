using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Configuracion;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Despliegues;

public sealed record ResultadoServidor(bool Ok, string Mensaje, List<string> Pasos);

/// <summary>
/// Servidores para despliegues. La contraseña de root se usa una sola vez (al preparar) y no se guarda:
/// GAS-Admin crea el usuario gasdeploy (grupo docker), le instala una llave propia y desde entonces
/// entra solo con esa llave. La dirección y la llave privada quedan cifradas en LlavesConfiguracion.
/// </summary>
public class ServicioServidores
{
    /// <summary>Comentario de la llave en authorized_keys; al volver a preparar se reemplaza la anterior.</summary>
    public const string ComentarioLlave = "gas-admin-despliegue";

    private readonly GasDbContext _db;
    private readonly ServicioLlaves _llaves;
    private readonly IConfiguration _config;
    private readonly IAuditoria _auditoria;
    private readonly ILogger<ServicioServidores> _logger;

    public ServicioServidores(GasDbContext db, ServicioLlaves llaves, IConfiguration config, IAuditoria auditoria, ILogger<ServicioServidores> logger)
    {
        _db = db;
        _llaves = llaves;
        _config = config;
        _auditoria = auditoria;
        _logger = logger;
    }

    public string? Host(int servidorId) => _config[Servidor.ClaveHost(servidorId)];
    public bool TieneLlave(int servidorId) => !string.IsNullOrWhiteSpace(_config[Servidor.ClaveLlavePrivada(servidorId)]);

    /// <summary>Guarda (cifrada) la dirección del servidor.</summary>
    public Task<string?> GuardarHostAsync(int servidorId, string host, string nombre) =>
        _llaves.GuardarAsync(Servidor.ClaveHost(servidorId), host.Trim(), esSecreto: false, $"Servidor {nombre}: dirección");

    public Task QuitarLlavesAsync(Servidor s) => _llaves.QuitarPrefijoAsync(Servidor.PrefijoLlaves(s.Id), $"Servidor {s.Nombre}");

    /// <summary>Conexión con la llave de GAS-Admin (para desplegar). Lanza ErrorSsh si no se puede.</summary>
    public async Task<ClienteSsh> ConectarAsync(Servidor s, CancellationToken ct)
    {
        var host = Host(s.Id);
        var llave = _config[Servidor.ClaveLlavePrivada(s.Id)];
        if (string.IsNullOrWhiteSpace(host)) throw new ErrorSsh($"El servidor {s.Nombre} no tiene dirección.");
        if (string.IsNullOrWhiteSpace(llave) || string.IsNullOrWhiteSpace(s.HuellaHost) || s.Estado == EstadoServidor.SinPreparar)
            throw new ErrorSsh($"El servidor {s.Nombre} no está preparado: usa Preparar con la contraseña de root.");
        var cliente = ClienteSsh.ConLlave(host, s.Puerto, s.Usuario, llave, s.HuellaHost);
        try
        {
            await cliente.ConectarAsync(ct);
            return cliente;
        }
        catch
        {
            cliente.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Prepara el servidor: entra con usuario y contraseña de administración, crea gasdeploy, le instala la
    /// llave de GAS-Admin y comprueba que ya puede entrar con ella. La contraseña no se guarda.
    /// </summary>
    public async Task<ResultadoServidor> PrepararAsync(int servidorId, string usuarioAdmin, string contrasena, string realizo, CancellationToken ct)
    {
        var s = await _db.Servidores.FirstAsync(x => x.Id == servidorId, ct);
        var pasos = new List<string>();
        var host = Host(s.Id);
        if (string.IsNullOrWhiteSpace(host)) return new(false, "Captura primero la dirección del servidor.", pasos);
        usuarioAdmin = usuarioAdmin.Trim();

        var (privada, publica) = ClienteSsh.GenerarLlave(ComentarioLlave);
        string huella;

        // 1) Entrar como administrador (con contraseña) e instalar el usuario y la llave.
        using (var admin = ClienteSsh.ConContrasena(host, s.Puerto, usuarioAdmin, contrasena, huellaEsperada: null))
        {
            try { await admin.ConectarAsync(ct); }
            catch (ErrorSsh ex) { return await FallaAsync(s, ex.Message, pasos); }
            huella = admin.HuellaRecibida!;
            pasos.Add($"Conectado como {usuarioAdmin}. Huella del servidor: {huella}");
            if (s.HuellaHost is not null && s.HuellaHost != huella)
                pasos.Add($"Atención: la huella cambió (antes {s.HuellaHost}). Se registra la nueva.");

            var prefijo = usuarioAdmin == "root" ? "" : "sudo -n ";
            var script = $$"""
                set -e
                U={{Servidor.UsuarioDespliegue}}
                command -v docker >/dev/null 2>&1 || { echo "FALTA_DOCKER"; exit 20; }
                id -u "$U" >/dev/null 2>&1 || { useradd -m -s /bin/bash "$U"; echo "Usuario $U creado."; }
                usermod -aG docker "$U"
                H=$(getent passwd "$U" | cut -d: -f6)
                install -d -m 700 -o "$U" -g "$U" "$H/.ssh" "$H/gas"
                touch "$H/.ssh/authorized_keys"
                grep -v ' {{ComentarioLlave}}$' "$H/.ssh/authorized_keys" > "$H/.ssh/ak.tmp" || true
                echo {{Sh.Q(publica)}} >> "$H/.ssh/ak.tmp"
                mv "$H/.ssh/ak.tmp" "$H/.ssh/authorized_keys"
                chown "$U:$U" "$H/.ssh/authorized_keys"
                chmod 600 "$H/.ssh/authorized_keys"
                echo "Llave de GAS-Admin instalada para $U."
                """;
            var r = await admin.EjecutarAsync(prefijo + "bash -c " + Sh.Q(script), pasos.Add, TimeSpan.FromMinutes(2), ct);
            if (!r.Ok)
            {
                var motivo = r.Salida.Contains("FALTA_DOCKER") ? "El servidor no tiene Docker instalado."
                           : r.Salida.Contains("sudo") ? $"{usuarioAdmin} no puede usar sudo sin contraseña; usa root."
                           : $"No se pudo preparar el servidor (código {r.Codigo}).";
                return await FallaAsync(s, motivo, pasos);
            }
        }

        // 2) Comprobar que ya entra con la llave y que puede usar Docker.
        using (var deploy = ClienteSsh.ConLlave(host, s.Puerto, Servidor.UsuarioDespliegue, privada, huella))
        {
            try { await deploy.ConectarAsync(ct); }
            catch (ErrorSsh ex) { return await FallaAsync(s, $"La llave se instaló pero no se pudo entrar con ella: {ex.Message}", pasos); }
            var info = await deploy.EjecutarAsync(ComandoInfo, null, TimeSpan.FromMinutes(1), ct);
            if (!info.Ok) return await FallaAsync(s, "El usuario de despliegue no puede usar Docker todavía. Vuelve a intentar en un minuto.", pasos);
            s.Sistema = Recortar(info.Salida.Trim().Replace('\n', ' '), 300);
            pasos.Add($"Entró como {Servidor.UsuarioDespliegue} con la llave. {s.Sistema}");
        }

        // 3) Guardar la llave (cifrada) y la huella. La contraseña se descarta aquí.
        var error = await _llaves.GuardarAsync(Servidor.ClaveLlavePrivada(s.Id), privada, esSecreto: true, $"Servidor {s.Nombre}: llave de despliegue");
        if (error is not null) return await FallaAsync(s, error, pasos);
        s.Usuario = Servidor.UsuarioDespliegue;
        s.HuellaHost = huella;
        s.Estado = EstadoServidor.Listo;
        s.PreparadoUtc = s.UltimaConexionUtc = DateTime.UtcNow;
        s.UltimoError = null;
        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync(AccionesAuditoria.ServidorPreparado,
            detalle: $"{s.Nombre}: usuario {Servidor.UsuarioDespliegue} con llave de GAS-Admin; huella {huella}. Preparado con la cuenta {usuarioAdmin}; la contraseña no se guardó.",
            entidad: "Servidor", entidadId: s.Id.ToString());
        _logger.LogInformation("Servidor {Servidor} preparado por {Usuario}.", s.Nombre, realizo);
        return new(true, "Servidor listo: GAS-Admin ya entra con su propia llave. La contraseña no se guardó.", pasos);
    }

    /// <summary>Prueba la conexión con la llave y actualiza la información del sistema.</summary>
    public async Task<ResultadoServidor> ProbarAsync(int servidorId, CancellationToken ct)
    {
        var s = await _db.Servidores.FirstAsync(x => x.Id == servidorId, ct);
        var pasos = new List<string>();
        try
        {
            using var c = await ConectarAsync(s, ct);
            var info = await c.EjecutarAsync(ComandoInfo, null, TimeSpan.FromMinutes(1), ct);
            if (!info.Ok) return await FallaAsync(s, "Se conectó, pero el usuario de despliegue no puede usar Docker.", pasos);
            s.Sistema = Recortar(info.Salida.Trim().Replace('\n', ' '), 300);
            s.Estado = EstadoServidor.Listo;
            s.UltimaConexionUtc = DateTime.UtcNow;
            s.UltimoError = null;
            await _db.SaveChangesAsync(ct);
            pasos.Add(s.Sistema ?? "");
            return new(true, "Conexión correcta.", pasos);
        }
        catch (ErrorSsh ex)
        {
            return await FallaAsync(s, ex.Message, pasos);
        }
    }

    /// <summary>Versión de Docker, sistema y espacio libre en la carpeta de trabajo.</summary>
    private const string ComandoInfo =
        "V=$(docker version --format '{{.Server.Version}}') || exit 3; " +
        "echo \"Docker $V · $(uname -sm) · libre: $(df -h \"$HOME\" | awk 'NR==2{print $4}')\"";

    private async Task<ResultadoServidor> FallaAsync(Servidor s, string mensaje, List<string> pasos)
    {
        pasos.Add(mensaje);
        s.UltimoError = Recortar(mensaje, 1000);
        if (s.Estado == EstadoServidor.Listo) s.Estado = EstadoServidor.ConError;
        await _db.SaveChangesAsync(CancellationToken.None);
        return new(false, mensaje, pasos);
    }

    private static string? Recortar(string? v, int max) => v is null ? null : v.Length > max ? v[..max] : v;
}
