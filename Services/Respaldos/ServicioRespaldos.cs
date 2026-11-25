using System.Security.Cryptography;
using System.Text;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Correo;
using GAS.Admin.Services.Despliegues;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Respaldos;

/// <summary>Una base que se respalda: de qué producto, en qué servidor y con qué credenciales.</summary>
public sealed record BaseRespaldable(string Producto, string Ambiente, Servidor Servidor, OrigenBaseDatos Origen);

/// <summary>
/// Respaldos de las bases de datos fuera del servidor:
///  1. Entra por SSH a cada servidor de despliegue y respalda cada base con BACKUP DATABASE … WITH CHECKSUM
///     (con el usuario de la app, que es db_owner de su base).
///  2. Saca el .bak del contenedor, lo descarga, lo comprime y lo cifra (CifradoRespaldo).
///  3. Lo sube a Backblaze B2 con bloqueo de gobierno hasta su fecha de retención.
///  4. Borra de B2 lo que ya venció y avisa por correo si algo falló.
/// Las bases salen solas de los ambientes con despliegue automático (su variable ConnectionStrings__*).
/// </summary>
public class ServicioRespaldos
{
    public const string ClaveBucket = "Respaldos:B2:Bucket", ClaveIdLlave = "Respaldos:B2:IdLlave", ClaveLlave = "Respaldos:B2:Llave",
        ClaveCifrado = "Respaldos:LlaveCifrado", ClaveHora = "Respaldos:Hora", ClaveCorreo = "Respaldos:CorreoAlertas";

    private const string CarpetaRemota = "gas/respaldos";
    private static readonly TimeSpan LimiteRespaldo = TimeSpan.FromMinutes(30);

    private readonly GasDbContext _db;
    private readonly ServicioDespliegues _despliegues;
    private readonly ServicioServidores _servidores;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly IAuditoria _auditoria;
    private readonly IServicioCorreo _correo;
    private readonly PlantillaCorreo _plantilla;
    private readonly ProgramadorRespaldos _programador;
    private readonly ILogger<ServicioRespaldos> _logger;

    private readonly StringBuilder _bitacora = new();
    private int _ejecucionId;

    public ServicioRespaldos(GasDbContext db, ServicioDespliegues despliegues, ServicioServidores servidores, IConfiguration config,
        IHttpClientFactory http, IAuditoria auditoria, IServicioCorreo correo, PlantillaCorreo plantilla, ProgramadorRespaldos programador,
        ILogger<ServicioRespaldos> logger)
    {
        _db = db;
        _despliegues = despliegues;
        _servidores = servidores;
        _config = config;
        _http = http;
        _auditoria = auditoria;
        _correo = correo;
        _plantilla = plantilla;
        _programador = programador;
        _logger = logger;
    }

    // ── Configuración ───────────────────────────────────────────────────────

    public bool B2Configurado => new[] { ClaveBucket, ClaveIdLlave, ClaveLlave }.All(k => !string.IsNullOrWhiteSpace(_config[k]));
    public bool CifradoConfigurado => (_config[ClaveCifrado]?.Trim().Length ?? 0) >= 32;
    public bool Configurado => B2Configurado && CifradoConfigurado;
    public string? Bucket => _config[ClaveBucket];
    public string? CorreoAlertas => _config[ClaveCorreo] is { Length: > 0 } c ? c.Trim() : null;
    public TimeOnly Hora => TimeOnly.TryParseExact(_config[ClaveHora]?.Trim(), "HH:mm", out var h) ? h : new TimeOnly(3, 0);

    public static TimeSpan Retencion(TipoRespaldo tipo) => tipo switch
    {
        TipoRespaldo.Mensual => TimeSpan.FromDays(366),
        TipoRespaldo.Semanal => TimeSpan.FromDays(56),
        _ => TimeSpan.FromDays(14)
    };

    /// <summary>El 1 de cada mes se guarda un mensual; los domingos, un semanal; los demás días, un diario.</summary>
    public static TipoRespaldo TipoDelDia(DateOnly fecha) =>
        fecha.Day == 1 ? TipoRespaldo.Mensual : fecha.DayOfWeek == DayOfWeek.Sunday ? TipoRespaldo.Semanal : TipoRespaldo.Diario;

    /// <summary>Bases a respaldar según los ambientes con despliegue automático, sin repetir (web y API suelen compartir).</summary>
    public async Task<(List<BaseRespaldable> Bases, List<string> Avisos)> BasesAsync(CancellationToken ct = default)
    {
        var configs = await _db.ConfiguracionesDespliegue.AsNoTracking()
            .Include(c => c.Ambiente).ThenInclude(a => a.Producto)
            .Include(c => c.Servidor)
            .OrderByDescending(c => c.Migraciones).ThenBy(c => c.Ambiente.Producto.Nombre).ThenBy(c => c.Ambiente.Nombre)
            .ToListAsync(ct);
        var bases = new List<BaseRespaldable>();
        var avisos = new List<string>();
        var vistas = new HashSet<string>();
        foreach (var c in configs)
        {
            var vars = _despliegues.Variables(c.AmbienteId);
            if (!vars.Keys.Any(k => k.StartsWith("ConnectionStrings__", StringComparison.Ordinal))) continue; // ese ambiente no usa base
            var (origen, error) = OrigenBaseDatos.Resolver(c, vars);
            if (origen is null)
            {
                avisos.Add($"{c.Ambiente.Producto.Nombre} · {c.Ambiente.Nombre}: {error}");
                continue;
            }
            if (vistas.Add($"{c.ServidorId}/{origen.Llave}"))
                bases.Add(new BaseRespaldable(c.Ambiente.Producto.Nombre, c.Ambiente.Nombre, c.Servidor, origen));
        }
        return (bases, avisos);
    }

    // ── Inicio ──────────────────────────────────────────────────────────────

    public async Task<(EjecucionRespaldo? Ejecucion, string? Error)> IniciarManualAsync(string usuario)
    {
        if (!Configurado) return (null, "Primero configura Backblaze B2 y la llave de cifrado.");
        if (await _db.EjecucionesRespaldo.AnyAsync(e => e.Estado == EstadoRespaldo.EnCurso))
            return (null, "Ya hay un respaldo en curso; espera a que termine.");
        var e = new EjecucionRespaldo { Tipo = TipoRespaldo.Manual, IniciadoPor = usuario };
        _db.EjecucionesRespaldo.Add(e);
        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync(AccionesAuditoria.RespaldoIniciado, $"Respaldo manual #{e.Id}", "EjecucionRespaldo", e.Id.ToString());
        _programador.Encolar(e.Id);
        return (e, null);
    }

    /// <summary>Crea la corrida programada del día; null si otra instancia ya la creó.</summary>
    public async Task<EjecucionRespaldo?> CrearProgramadaAsync(DateOnly fechaLocal, CancellationToken ct)
    {
        var clave = fechaLocal.ToString("yyyy-MM-dd");
        if (await _db.EjecucionesRespaldo.AnyAsync(e => e.ClaveProgramada == clave, ct)) return null;
        var e = new EjecucionRespaldo { Tipo = TipoDelDia(fechaLocal), ClaveProgramada = clave, IniciadoPor = "Programado" };
        _db.EjecucionesRespaldo.Add(e);
        try
        {
            await _db.SaveChangesAsync(ct);
            return e;
        }
        catch (DbUpdateException)
        {
            _db.Entry(e).State = EntityState.Detached;
            return null; // la creó otra instancia al mismo tiempo (índice único)
        }
    }

    // ── Ejecución ───────────────────────────────────────────────────────────

    public async Task EjecutarAsync(int ejecucionId, CancellationToken ct)
    {
        var e = await _db.EjecucionesRespaldo.FirstOrDefaultAsync(x => x.Id == ejecucionId, ct);
        if (e is null || e.Estado != EstadoRespaldo.EnCurso) return;
        _ejecucionId = e.Id;
        e.InicioUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        int correctas = 0, fallidas = 0;
        try
        {
            Linea($"Respaldo #{e.Id} ({Etiquetas.Texto(e.Tipo).ToLowerInvariant()}) · por {e.IniciadoPor}");
            if (!Configurado) throw new InvalidOperationException("Falta configurar Backblaze B2 o la llave de cifrado.");

            var (bases, avisos) = await BasesAsync(ct);
            foreach (var a in avisos) Linea("⚠ " + a);
            if (bases.Count == 0) throw new InvalidOperationException("No hay bases que respaldar: ningún ambiente con despliegue automático tiene ConnectionStrings__*.");
            Linea($"Bases: {string.Join(", ", bases.Select(b => $"{b.Origen.BaseDatos} ({b.Producto})"))}");

            var b2 = new ClienteB2(_http.CreateClient("respaldos"));
            Linea($"Conectando con Backblaze B2 (bucket {Bucket})…");
            await b2.AutorizarAsync(_config[ClaveIdLlave]!, _config[ClaveLlave]!, Bucket!, ct);
            Linea("Autorizado.");
            var retenerHasta = DateTime.UtcNow + Retencion(e.Tipo);

            foreach (var grupo in bases.GroupBy(b => b.Servidor.Id))
            {
                var servidor = grupo.First().Servidor;
                Titulo(servidor.Nombre);
                ClienteSsh ssh;
                try
                {
                    ssh = await _servidores.ConectarAsync(servidor, ct);
                }
                catch (ErrorSsh ex)
                {
                    Linea($"✖ {ex.Message}");
                    foreach (var b in grupo) RegistrarError(e, b, ex.Message);
                    fallidas += grupo.Count();
                    continue;
                }
                using (ssh)
                {
                    await ssh.EjecutarAsync($"mkdir -p \"$HOME\"/{CarpetaRemota} && chmod 700 \"$HOME\"/{CarpetaRemota}", null, TimeSpan.FromSeconds(30), ct);
                    foreach (var b in grupo)
                    {
                        try
                        {
                            await RespaldarBaseAsync(ssh, e, b, b2, retenerHasta, ct);
                            correctas++;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                        {
                            fallidas++;
                            Linea($"✖ {b.Origen.BaseDatos}: {ex.Message}");
                            if (ex is not (ErrorB2 or ErrorSsh or InvalidOperationException or IOException))
                                _logger.LogError(ex, "Respaldo #{Id}: error al respaldar {Base}.", e.Id, b.Origen.BaseDatos);
                            RegistrarError(e, b, ex.Message);
                        }
                        await _db.SaveChangesAsync(CancellationToken.None);
                        await GuardarBitacoraAsync();
                    }
                }
            }

            Titulo("Limpieza");
            await LimpiarAsync(b2, ct);

            e.Estado = fallidas == 0 ? EstadoRespaldo.Exitoso : correctas > 0 ? EstadoRespaldo.ConErrores : EstadoRespaldo.Fallido;
            e.Resumen = fallidas == 0
                ? $"{correctas} base(s) respaldada(s) en B2; se conservan hasta {Fechas.Formato(retenerHasta)}."
                : $"{correctas} correcta(s), {fallidas} con error. Revisa la bitácora.";
            Linea((fallidas == 0 ? "✔ " : "✖ ") + e.Resumen);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            if (ex is not (ErrorB2 or ErrorSsh or InvalidOperationException)) _logger.LogError(ex, "Respaldo #{Id} falló.", e.Id);
            Linea("✖ " + ex.Message);
            e.Estado = correctas > 0 ? EstadoRespaldo.ConErrores : EstadoRespaldo.Fallido;
            e.Resumen = Recortar(ex.Message, 1000);
        }
        finally
        {
            if (e.Estado == EstadoRespaldo.EnCurso)
            {
                e.Estado = EstadoRespaldo.Interrumpido;
                e.Resumen = "GAS-Admin se detuvo durante el respaldo.";
            }
            e.FinUtc = DateTime.UtcNow;
            e.Bitacora = _bitacora.ToString();
            await _db.SaveChangesAsync(CancellationToken.None);
            await _auditoria.RegistrarAsync(AccionesAuditoria.RespaldoTerminado,
                $"#{e.Id} {Etiquetas.Texto(e.Estado)}: {e.Resumen}", "EjecucionRespaldo", e.Id.ToString(), email: e.IniciadoPor.Contains('@') ? e.IniciadoPor : null);
            if (e.Estado != EstadoRespaldo.Exitoso) await AlertarAsync(e);
        }
    }

    private async Task RespaldarBaseAsync(ClienteSsh ssh, EjecucionRespaldo e, BaseRespaldable b, ClienteB2 b2, DateTime retenerHasta, CancellationToken ct)
    {
        var o = b.Origen;
        var ahora = DateTime.UtcNow;
        var archivo = $"gasr-{EjecutorDespliegue.Slug(o.BaseDatos)}-{ahora:yyyyMMdd-HHmmss}.bak";
        var remoto = $"{CarpetaRemota}/{archivo}";
        var env = $"\"$HOME\"/{CarpetaRemota}/respaldo.env";
        var q = Sh.Q(o.Contenedor);
        Linea($"{o.BaseDatos} ({b.Producto}): respaldando en el contenedor {o.Contenedor}…");

        // La contraseña va en un archivo 0600 que el mismo comando borra; nunca en la línea de comandos.
        await using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(o.ArchivoEnv)))
            await ssh.SubirAsync(ms, $"{CarpetaRemota}/respaldo.env", 600, ct);
        var r = await ssh.EjecutarAsync(
            o.ComandoRespaldo(env, archivo, conChecksum: true) +
            $" && docker cp {q}:/var/opt/mssql/backup/{archivo} \"$HOME\"/{remoto} && chmod 600 \"$HOME\"/{remoto}; RC=$?; " +
            $"rm -f {env}; docker exec {q} rm -f /var/opt/mssql/backup/{archivo}; exit $RC",
            l => { if (!string.IsNullOrWhiteSpace(l)) Linea("   " + l.Trim()); }, LimiteRespaldo, ct);
        if (!r.Ok)
        {
            await ssh.EjecutarAsync($"rm -f {env} \"$HOME\"/{remoto}", null, TimeSpan.FromSeconds(30), CancellationToken.None);
            throw new InvalidOperationException("SQL Server no pudo hacer el respaldo (revisa los mensajes de arriba).");
        }

        var temporal = Path.Combine(Path.GetTempPath(), $"gas-respaldo-{e.Id}-{archivo}");
        var cifrado = temporal + ".gasr";
        try
        {
            await using (var f = File.Create(temporal))
                await ssh.DescargarAsync(remoto, f, ct);
            await ssh.EjecutarAsync($"rm -f \"$HOME\"/{remoto}", null, TimeSpan.FromSeconds(30), CancellationToken.None);

            var original = new FileInfo(temporal).Length;
            string sha;
            await using (var f = File.OpenRead(temporal))
                sha = Convert.ToHexStringLower(await SHA256.HashDataAsync(f, ct));
            await using (var origen = File.OpenRead(temporal))
            await using (var destino = new FileStream(cifrado, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                await CifradoRespaldo.CifrarAsync(origen, destino, _config[ClaveCifrado]!, ct);
            var tamano = new FileInfo(cifrado).Length;

            var local = Fechas.ALocal(ahora);
            var nombre = $"{EjecutorDespliegue.Slug(b.Producto)}/{o.BaseDatos}/{local:yyyy-MM-dd_HHmmss}-{e.Tipo.ToString().ToLowerInvariant()}{CifradoRespaldo.Extension}";
            Linea($"   {Mb(original)} → {Mb(tamano)} comprimido y cifrado. Subiendo a B2…");
            var (subido, conBloqueo) = await b2.SubirAsync(nombre, cifrado, retenerHasta, ct);
            if (!conBloqueo) Linea("   ⚠ La llave de B2 no puede fijar bloqueos por archivo: queda la retención por defecto del bucket.");

            _db.ArchivosRespaldo.Add(new ArchivoRespaldo
            {
                EjecucionId = e.Id, Producto = b.Producto, BaseDatos = o.BaseDatos, Nombre = nombre, IdArchivoB2 = subido.FileId,
                BytesOriginal = original, Bytes = tamano, Sha256 = sha, RetenerHastaUtc = retenerHasta
            });
            Linea($"✔ {o.BaseDatos}: {nombre}");
        }
        finally
        {
            foreach (var t in new[] { temporal, cifrado })
                try { File.Delete(t); } catch (IOException) { }
        }
    }

    /// <summary>Borra de B2 lo que ya pasó su fecha de retención (B2 no deja borrar antes).</summary>
    private async Task LimpiarAsync(ClienteB2 b2, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var vencidos = await _db.ArchivosRespaldo
            .Where(a => a.BorradoUtc == null && a.IdArchivoB2 != null && a.RetenerHastaUtc < ahora)
            .OrderBy(a => a.RetenerHastaUtc).Take(500).ToListAsync(ct);
        if (vencidos.Count == 0)
        {
            Linea("Nada vencido que borrar.");
            return;
        }
        int borrados = 0, bloqueados = 0;
        foreach (var a in vencidos)
        {
            try
            {
                if (await b2.BorrarAsync(a.Nombre, a.IdArchivoB2!, ct)) { a.BorradoUtc = DateTime.UtcNow; borrados++; }
                else bloqueados++;
            }
            catch (ErrorB2 ex)
            {
                Linea($"⚠ No se pudo borrar {a.Nombre}: {ex.Message}");
            }
        }
        await _db.SaveChangesAsync(CancellationToken.None);
        Linea($"Vencidos borrados: {borrados}{(bloqueados > 0 ? $"; {bloqueados} siguen bloqueados en B2 y se reintentarán" : "")}.");
    }

    // ── Descarga ────────────────────────────────────────────────────────────

    /// <summary>Descarga de B2, verifica, descifra y escribe el .bak original en <paramref name="destino"/>.</summary>
    public async Task DescargarAsync(ArchivoRespaldo a, Stream destino, CancellationToken ct)
    {
        if (!Configurado || a.IdArchivoB2 is null) throw new InvalidOperationException("Ese respaldo no está disponible.");
        var b2 = new ClienteB2(_http.CreateClient("respaldos"));
        await b2.AutorizarAsync(_config[ClaveIdLlave]!, _config[ClaveLlave]!, Bucket!, ct);
        var temporal = Path.Combine(Path.GetTempPath(), $"gas-descarga-{a.Id}-{Guid.NewGuid():N}.gasr");
        try
        {
            await using (var f = File.Create(temporal))
                await b2.DescargarAsync(a.Nombre, f, ct);
            await using var origen = File.OpenRead(temporal);
            await CifradoRespaldo.DescifrarAsync(origen, destino, _config[ClaveCifrado]!, ct);
        }
        finally
        {
            try { File.Delete(temporal); } catch (IOException) { }
        }
    }

    // ── Auxiliares ──────────────────────────────────────────────────────────

    private void RegistrarError(EjecucionRespaldo e, BaseRespaldable b, string error) =>
        _db.ArchivosRespaldo.Add(new ArchivoRespaldo
        {
            EjecucionId = e.Id, Producto = b.Producto, BaseDatos = b.Origen.BaseDatos, Error = Recortar(error, 1000),
            RetenerHastaUtc = DateTime.UtcNow
        });

    private async Task AlertarAsync(EjecucionRespaldo e)
    {
        var para = CorreoAlertas;
        if (para is null || !_correo.Configurado) return;
        try
        {
            var url = Uri.TryCreate(_config["Seguridad:UrlPublica"], UriKind.Absolute, out var u) ? new Uri(u, $"/Infraestructura/Respaldos/Ejecucion/{e.Id}").ToString() : null;
            var html = await _plantilla.HtmlAsync(new ContenidoCorreo($"Respaldo #{e.Id}: {Etiquetas.Texto(e.Estado).ToLowerInvariant()}", "Respaldos",
                MensajeTexto: $"El respaldo {Etiquetas.Texto(e.Tipo).ToLowerInvariant()} de las bases de datos no terminó bien.\n\n{e.Resumen}",
                Datos: [new("Estado", Etiquetas.Texto(e.Estado), true), new("Inicio", Fechas.Formato(e.InicioUtc)), new("Bucket", Bucket ?? "—")],
                Boton: url is null ? null : new BotonCorreo("Ver la bitácora", url),
                Nota: "Este aviso lo envía GAS-Admin cuando un respaldo automático falla."));
            var r = await _correo.EnviarAsync(new MensajeCorreo([para], [], $"⚠ Respaldo de bases #{e.Id}: {Etiquetas.Texto(e.Estado)}", html,
                $"El respaldo #{e.Id} no terminó bien: {e.Resumen}", [], null));
            if (!r.Exito) _logger.LogWarning("No se pudo enviar la alerta del respaldo #{Id}: {Error}", e.Id, r.Error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar la alerta del respaldo #{Id}.", e.Id);
        }
    }

    private void Titulo(string texto) => Linea($"── {texto} ──");

    private void Linea(string texto) => _bitacora.Append(Fechas.ALocal(DateTime.UtcNow).ToString("HH:mm:ss")).Append("  ").AppendLine(texto);

    private async Task GuardarBitacoraAsync()
    {
        var texto = _bitacora.ToString();
        await _db.EjecucionesRespaldo.Where(x => x.Id == _ejecucionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Bitacora, texto));
    }

    public static string Mb(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.0} MB" : $"{bytes / 1024.0:0} KB";

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
}
