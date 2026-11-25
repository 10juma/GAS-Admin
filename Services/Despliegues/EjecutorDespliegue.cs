using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Infraestructura;
using GAS.Admin.Services.Integraciones;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Despliegues;

/// <summary>
/// Ejecuta un despliegue en el servidor, paso a paso, dejando todo en la bitácora:
///  1. Resuelve la rama/commit en GitHub y sube el código al servidor (el servidor no necesita acceso a GitHub).
///  2. Compila las imágenes de Docker en el servidor.
///  3. Respalda la base y aplica las migraciones (si el grupo las tiene).
///  4. Reemplaza cada contenedor y verifica que responda; si alguno falla, regresa todos a su imagen anterior.
///  5. Limpia imágenes viejas y registra el despliegue en la bitácora de cada ambiente.
/// </summary>
public partial class EjecutorDespliegue
{
    private const int MaxBitacora = 400_000;
    private static readonly TimeSpan LimiteCompilacion = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LimiteComando = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EsperaSalud = TimeSpan.FromSeconds(90);

    private readonly GasDbContext _db;
    private readonly ServicioServidores _servidores;
    private readonly ServicioDespliegues _despliegues;
    private readonly ServicioInfraestructura _infra;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly IServiceScopeFactory _scopes;
    private readonly IAuditoria _auditoria;
    private readonly ILogger<EjecutorDespliegue> _logger;

    private readonly StringBuilder _bitacora = new();
    private readonly object _candado = new();
    private bool _sucia;

    public EjecutorDespliegue(GasDbContext db, ServicioServidores servidores, ServicioDespliegues despliegues, ServicioInfraestructura infra,
        IConfiguration config, IHttpClientFactory http, IServiceScopeFactory scopes, IAuditoria auditoria, ILogger<EjecutorDespliegue> logger)
    {
        _db = db;
        _servidores = servidores;
        _despliegues = despliegues;
        _infra = infra;
        _config = config;
        _http = http;
        _scopes = scopes;
        _auditoria = auditoria;
        _logger = logger;
    }

    private sealed class Paso(string mensaje) : Exception(mensaje);

    /// <summary>Contenedor ya reemplazado (para poder regresarlo si otro falla).</summary>
    private sealed record Cambiado(ConfiguracionDespliegue Config, string? ImagenAnterior, string ComandoRun);

    public async Task EjecutarAsync(int ejecucionId, CancellationToken ct)
    {
        var e = await _db.EjecucionesDespliegue.Include(x => x.Servidor).Include(x => x.Producto).FirstOrDefaultAsync(x => x.Id == ejecucionId, ct);
        if (e is null || e.Estado != EstadoEjecucion.EnCola) return;
        e.Estado = EstadoEjecucion.EnCurso;
        e.InicioUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        using var detener = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var guardado = GuardarBitacoraPeriodicamenteAsync(e.Id, detener.Token);

        var cambiados = new List<Cambiado>();
        var configs = new List<ConfiguracionDespliegue>();
        var revertido = false;
        try
        {
            var ids = e.IdsAmbientes();
            configs = await _db.ConfiguracionesDespliegue.AsNoTracking()
                .Include(c => c.Ambiente).ThenInclude(a => a.Repositorio)
                .Where(c => ids.Contains(c.AmbienteId))
                .OrderByDescending(c => c.Migraciones).ThenBy(c => c.Ambiente.Plataforma)
                .ToListAsync(ct);
            if (configs.Count == 0) throw new Paso("Los ambientes ya no tienen configuración de despliegue.");

            Linea($"Despliegue #{e.Id} de {e.Producto.Nombre}: {string.Join(", ", configs.Select(c => c.Ambiente.Nombre))} · {e.Referencia} · por {e.IniciadoPor}");

            // ── 1. Código ──────────────────────────────────────────────────────
            var repo = ServicioIntegraciones.NombreGitHub(configs[0].Ambiente.Repositorio?.Url)
                       ?? throw new Paso("El ambiente no tiene un repositorio de GitHub.");
            Titulo("Código");
            var (sha, mensaje) = await ResolverCommitAsync(repo, e.Referencia, ct);
            e.Commit = sha;
            e.MensajeCommit = Recortar(mensaje.Split('\n')[0], 500);
            await _db.SaveChangesAsync(ct);
            var tag = sha[..12];
            Linea($"{repo} @ {e.Referencia} → commit {sha[..7]}: {e.MensajeCommit}");

            Linea($"Conectando con {e.Servidor.Nombre}…");
            using var ssh = await _servidores.ConectarAsync(e.Servidor, ct);
            Linea("Conectado (huella verificada).");

            var carpeta = $"gas/{Slug(e.Producto.Nombre)}";
            var dir = $"\"$HOME\"/{carpeta}";
            await Remoto(ssh, $"mkdir -p {dir} && rm -rf {dir}/src && mkdir -p {dir}/src && " +
                              $"echo \"Espacio libre: $(df -h \"$HOME\" | awk 'NR==2{{print $4}}')\"", "Preparar carpeta", ct);

            var tarball = Path.Combine(Path.GetTempPath(), $"gas-despliegue-{e.Id}.tar.gz");
            try
            {
                var bytes = await DescargarCodigoAsync(repo, sha, tarball, ct);
                Linea($"Código descargado de GitHub ({bytes / 1024.0 / 1024.0:0.0} MB). Subiendo al servidor…");
                await using (var f = File.OpenRead(tarball))
                    await ssh.SubirAsync(f, $"{carpeta}/codigo.tar.gz", 600, ct);
            }
            finally
            {
                try { File.Delete(tarball); } catch (IOException) { }
            }
            await Remoto(ssh, $"tar -xzf {dir}/codigo.tar.gz -C {dir}/src --strip-components=1 && rm -f {dir}/codigo.tar.gz && echo 'Código listo.'",
                "Descomprimir código", ct);

            // ── 2. Compilación ─────────────────────────────────────────────────
            Titulo("Compilación");
            foreach (var c in configs.DistinctBy(c => (c.Dockerfile, c.Imagen)))
                await Compilar(ssh, dir, c.Dockerfile, $"{c.Imagen}:{tag}", ct);
            var mig = configs.FirstOrDefault(c => c.Migraciones);
            if (mig is not null)
            {
                if (string.IsNullOrWhiteSpace(mig.DockerfileMigraciones)) throw new Paso($"{mig.Ambiente.Nombre} aplica migraciones pero no tiene Dockerfile de migraciones.");
                await Compilar(ssh, dir, mig.DockerfileMigraciones, $"{ImagenMigraciones(mig)}:{tag}", ct);
            }

            // ── Variables (archivos .env, solo legibles por gasdeploy) ─────────
            foreach (var c in configs)
            {
                var vars = VariablesFinales(c, e.Producto.Id);
                await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(ServicioDespliegues.Escribir(vars)));
                await ssh.SubirAsync(ms, $"{carpeta}/{c.Contenedor}.env", 600, ct);
                Linea($"Variables de {c.Contenedor}: {vars.Count} ({string.Join(", ", vars.Keys)}).");
            }

            // ── 3. Respaldo y migraciones ──────────────────────────────────────
            if (mig is not null)
            {
                Titulo("Base de datos");
                if (mig.RespaldarBd) await RespaldarAsync(ssh, carpeta, dir, mig, e.Producto.Id, ct);
                else Linea("Sin respaldo previo (desactivado en la configuración).");
                var red = string.IsNullOrWhiteSpace(mig.Red) ? "" : $" --network {Sh.Q(mig.Red)}";
                var r = await ssh.EjecutarAsync($"docker run --rm{red} --env-file {dir}/{mig.Contenedor}.env {Sh.Q($"{ImagenMigraciones(mig)}:{tag}")}",
                    Linea, TimeSpan.FromMinutes(15), ct);
                if (!r.Ok) throw new Paso("Las migraciones fallaron; no se cambió ningún contenedor.");
                Linea("Migraciones aplicadas.");
            }

            // ── 4. Contenedores ────────────────────────────────────────────────
            Titulo("Contenedores");
            foreach (var c in configs)
            {
                var run = ComandoRun(c, dir, $"{c.Imagen}:{tag}");
                var r = await ssh.EjecutarAsync(
                    $"PREV=$(docker inspect -f '{{{{.Image}}}}' {Sh.Q(c.Contenedor)} 2>/dev/null || true); echo \"ANTERIOR=$PREV\"; " +
                    $"docker rm -f {Sh.Q(c.Contenedor)} >/dev/null 2>&1 || true; {run}",
                    Linea, LimiteComando, ct);
                var anterior = AnteriorRegex().Match(r.Salida) is { Success: true } m && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null;
                cambiados.Add(new Cambiado(c, anterior, ComandoRun(c, dir, "{IMAGEN}")));
                if (!r.Ok) throw new Paso($"No se pudo iniciar {c.Contenedor}.");
                Linea($"{c.Contenedor} iniciado con {c.Imagen}:{tag}. Verificando…");
                if (!await SaludableAsync(ssh, c, ct))
                {
                    await Remoto(ssh, $"docker logs --tail 60 {Sh.Q(c.Contenedor)}", $"Últimas líneas de {c.Contenedor}", ct, permitirFalla: true);
                    throw new Paso($"{c.Contenedor} no quedó funcionando.");
                }
                Linea($"{c.Contenedor} responde correctamente.");
            }

            // ── 5. Limpieza y registro ─────────────────────────────────────────
            Titulo("Limpieza");
            var limpieza = new StringBuilder($"rm -rf {dir}/src; ");
            foreach (var img in configs.Select(c => c.Imagen).Concat(mig is null ? [] : [ImagenMigraciones(mig)]).Distinct())
                limpieza.Append($"docker images {Sh.Q(img)} --format '{{{{.Repository}}}}:{{{{.Tag}}}}' | tail -n +4 | xargs -r docker rmi >/dev/null 2>&1; ");
            limpieza.Append("docker image prune -f >/dev/null 2>&1; docker builder prune -f --filter until=168h >/dev/null 2>&1; echo 'Se conservan las 3 versiones más recientes de cada imagen.'");
            await Remoto(ssh, limpieza.ToString(), "Limpieza", ct, permitirFalla: true);

            e.Estado = EstadoEjecucion.Exitoso;
            e.Resumen = $"Desplegado {sha[..7]} en {string.Join(", ", configs.Select(c => c.Ambiente.Nombre))}.";
            Linea("✔ " + e.Resumen);
        }
        catch (Exception ex) when (ex is Paso or ErrorSsh or HttpRequestException or IOException or TaskCanceledException && !ct.IsCancellationRequested
                                   || ex is Renci.SshNet.Common.SshException || ex is JsonException)
        {
            Linea("✖ " + ex.Message);
            if (ex is not Paso) _logger.LogWarning(ex, "Despliegue {Id} falló.", e.Id);
            e.Estado = EstadoEjecucion.Fallido;
            e.Resumen = Recortar(ex.Message, 1000);
            if (cambiados.Count > 0)
            {
                revertido = await RevertirAsync(e, cambiados, ct);
                e.Estado = revertido ? EstadoEjecucion.Revertido : EstadoEjecucion.Fallido;
                e.Resumen = Recortar($"{ex.Message} {(revertido ? "Se regresó a la versión anterior." : "No se pudo regresar a la versión anterior: revisa el servidor.")}", 1000);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Linea("✖ Error inesperado: " + ex.Message);
            _logger.LogError(ex, "Despliegue {Id}: error inesperado.", e.Id);
            e.Estado = EstadoEjecucion.Fallido;
            e.Resumen = Recortar("Error inesperado: " + ex.Message, 1000);
        }
        finally
        {
            e.FinUtc = DateTime.UtcNow;
            detener.Cancel();
            try { await guardado; } catch (OperationCanceledException) { }
            lock (_candado) e.Bitacora = _bitacora.ToString();
            await _db.SaveChangesAsync(CancellationToken.None);
            await RegistrarAsync(e, configs, cambiados.Count > 0);
        }
    }

    // ── Pasos ─────────────────────────────────────────────────────────────────

    private async Task Compilar(ClienteSsh ssh, string dir, string dockerfile, string imagen, CancellationToken ct)
    {
        Linea($"Compilando {imagen} ({dockerfile})…");
        var r = await ssh.EjecutarAsync(
            $"cd {dir}/src && test -f {Sh.Q(dockerfile)} || {{ echo 'No existe {dockerfile.Replace("'", "")} en el repositorio.'; exit 4; }}; " +
            // BuildKit solo si el servidor tiene el componente buildx; si no, el compilador clásico (el que usa
            // "docker build" a mano en ese caso). Forzar BuildKit sin buildx hace fallar la compilación.
            $"cd {dir}/src && if docker buildx version >/dev/null 2>&1; then " +
            $"DOCKER_BUILDKIT=1 docker build --pull --progress=plain -f {Sh.Q(dockerfile)} -t {Sh.Q(imagen)} .; " +
            $"else echo 'Docker sin buildx: se usa el compilador clásico.'; " +
            $"DOCKER_BUILDKIT=0 docker build --pull -f {Sh.Q(dockerfile)} -t {Sh.Q(imagen)} .; fi",
            Linea, LimiteCompilacion, ct);
        if (!r.Ok) throw new Paso($"La compilación de {imagen} falló; no se cambió ningún contenedor.");
        Linea($"Imagen {imagen} lista.");
    }

    private async Task RespaldarAsync(ClienteSsh ssh, string carpeta, string dir, ConfiguracionDespliegue c, int productoId, CancellationToken ct)
    {
        var vars = VariablesFinales(c, productoId);
        // DefaultConnection o Default (cada producto nombra distinto su cadena); si no, la única ConnectionStrings__* que haya.
        var clave = new[] { "ConnectionStrings__DefaultConnection", "ConnectionStrings__Default" }.FirstOrDefault(vars.ContainsKey)
                    ?? (vars.Keys.Where(k => k.StartsWith("ConnectionStrings__", StringComparison.Ordinal)).ToList() is [var unica] ? unica : null);
        if (clave is null)
            throw new Paso("Para respaldar se necesita la variable ConnectionStrings__DefaultConnection (o ConnectionStrings__Default) en el ambiente que migra.");
        SqlConnectionStringBuilder csb;
        try { csb = new SqlConnectionStringBuilder(vars[clave]); }
        catch (ArgumentException) { throw new Paso($"{clave} no es una cadena de conexión válida."); }

        var contenedor = string.IsNullOrWhiteSpace(c.ContenedorSql) ? csb.DataSource.Split(',')[0].Split('\\')[0] : c.ContenedorSql;
        var bd = string.IsNullOrWhiteSpace(c.BaseDatos) ? csb.InitialCatalog : c.BaseDatos;
        if (string.IsNullOrWhiteSpace(bd)) throw new Paso("No se sabe qué base respaldar: captura la base de datos en la configuración.");
        var archivo = $"gas-{Slug(bd)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak";
        Linea($"Respaldando la base {bd} (contenedor {contenedor}) en /var/opt/mssql/backup/{archivo}…");

        // La contraseña va en un archivo temporal (0600) y por variable de entorno, nunca en la línea de comandos.
        await using (var ms = new MemoryStream(Encoding.UTF8.GetBytes($"SQLCMDPASSWORD={Sh.Q(csb.Password)}\n")))
            await ssh.SubirAsync(ms, $"{carpeta}/respaldo.env", 600, ct);
        var sql = $"BACKUP DATABASE [{bd.Replace("]", "]]")}] TO DISK = N'/var/opt/mssql/backup/{archivo}' WITH COPY_ONLY, INIT";
        var q = Sh.Q(contenedor);
        var r = await ssh.EjecutarAsync(
            $"set -a; . {dir}/respaldo.env; set +a; rm -f {dir}/respaldo.env; " +
            $"P=/opt/mssql-tools18/bin/sqlcmd; C=-C; docker exec {q} test -x $P || {{ P=/opt/mssql-tools/bin/sqlcmd; C=; }}; " +
            $"docker exec {q} mkdir -p /var/opt/mssql/backup && " +
            $"docker exec -e SQLCMDPASSWORD {q} $P -S localhost -U {Sh.Q(csb.UserID)} $C -b -Q {Sh.Q(sql)}; RC=$?; " +
            $"docker exec {q} find /var/opt/mssql/backup -name 'gas-*.bak' -mtime +14 -delete >/dev/null 2>&1; exit $RC",
            Linea, TimeSpan.FromMinutes(20), ct);
        await ssh.EjecutarAsync($"rm -f {dir}/respaldo.env", null, LimiteComando, CancellationToken.None);
        if (!r.Ok)
            throw new Paso("El respaldo de la base falló; no se aplicaron migraciones ni se cambiaron contenedores.");
        Linea($"Respaldo listo: {archivo} (se conservan 14 días).");
    }

    private async Task<bool> SaludableAsync(ClienteSsh ssh, ConfiguracionDespliegue c, CancellationToken ct)
    {
        var limite = DateTime.UtcNow + EsperaSalud;
        var url = string.IsNullOrWhiteSpace(c.Ambiente.Url) ? null : c.Ambiente.Url.TrimEnd('/') + "/" + c.RutaSalud.TrimStart('/');
        DateTime? estableDesde = null;
        string? ultimo = null;
        while (DateTime.UtcNow < limite)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            var r = await ssh.EjecutarAsync($"docker inspect -f '{{{{.State.Status}}}} {{{{.RestartCount}}}}' {Sh.Q(c.Contenedor)}", null, LimiteComando, ct);
            var partes = r.Salida.Trim().Split(' ');
            if (!r.Ok || partes.Length < 2 || partes[1] != "0")
            {
                Linea($"El contenedor {c.Contenedor} se detuvo o se reinició ({r.Salida.Trim()}).");
                return false;
            }
            if (partes[0] != "running") { estableDesde = null; continue; }
            estableDesde ??= DateTime.UtcNow;
            if (DateTime.UtcNow - estableDesde < TimeSpan.FromSeconds(8)) continue;
            if (url is null) { Linea("Sin URL en el ambiente: se verificó solo que el contenedor siga corriendo."); return true; }
            try
            {
                using var corto = CancellationTokenSource.CreateLinkedTokenSource(ct);
                corto.CancelAfter(TimeSpan.FromSeconds(15));
                using var resp = await _http.CreateClient("despliegues").GetAsync(url, corto.Token);
                ultimo = $"{(int)resp.StatusCode}";
                if ((int)resp.StatusCode < 500) { Linea($"{url} respondió {(int)resp.StatusCode}."); return true; }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                ultimo = ex.Message;
            }
        }
        Linea($"{c.Contenedor} no respondió bien en {EsperaSalud.TotalSeconds:0} s{(url is null ? "" : $" ({url}: {ultimo})")}.");
        return false;
    }

    private async Task<bool> RevertirAsync(EjecucionDespliegue e, List<Cambiado> cambiados, CancellationToken ct)
    {
        Titulo("Reversa");
        var ok = true;
        try
        {
            using var ssh = await _servidores.ConectarAsync(e.Servidor, ct);
            foreach (var c in Enumerable.Reverse(cambiados))
            {
                if (c.ImagenAnterior is null)
                {
                    Linea($"{c.Config.Contenedor} no tenía versión anterior; se deja como quedó.");
                    ok = false;
                    continue;
                }
                var r = await ssh.EjecutarAsync($"docker rm -f {Sh.Q(c.Config.Contenedor)} >/dev/null 2>&1 || true; " +
                                                c.ComandoRun.Replace("{IMAGEN}", Sh.Q(c.ImagenAnterior)), Linea, LimiteComando, ct);
                if (r.Ok) Linea($"{c.Config.Contenedor} regresó a su imagen anterior.");
                else { Linea($"No se pudo regresar {c.Config.Contenedor}."); ok = false; }
            }
            if (cambiados.Any(c => c.Config.Migraciones))
                Linea("Las migraciones de base de datos ya aplicadas no se revierten (hay un respaldo previo si estaba activado).");
        }
        catch (Exception ex) when (ex is ErrorSsh or Renci.SshNet.Common.SshException or IOException)
        {
            Linea("No se pudo revertir: " + ex.Message);
            ok = false;
        }
        return ok;
    }

    /// <summary>Despliegue en la bitácora de cada ambiente (solo si se llegó a cambiar algún contenedor o si terminó bien).</summary>
    private async Task RegistrarAsync(EjecucionDespliegue e, List<ConfiguracionDespliegue> configs, bool huboCambios)
    {
        try
        {
            if (e.Commit is not null && (e.Estado == EstadoEjecucion.Exitoso || huboCambios))
            {
                foreach (var c in configs)
                {
                    var d = new Despliegue
                    {
                        AmbienteId = c.AmbienteId,
                        Version = e.Commit[..7],
                        FechaUtc = e.FinUtc ?? DateTime.UtcNow,
                        Resultado = e.Estado == EstadoEjecucion.Exitoso ? ResultadoDespliegue.Exitoso
                                  : e.Estado == EstadoEjecucion.Revertido ? ResultadoDespliegue.Revertido : ResultadoDespliegue.ConProblemas,
                        Referencia = Recortar(e.Referencia, 150),
                        Realizo = e.IniciadoPor,
                        ExternoId = $"gas-ejecucion:{e.Id}",
                        Notas = Recortar($"Despliegue automático #{e.Id}. {e.MensajeCommit} {(e.Estado == EstadoEjecucion.Exitoso ? "" : e.Resumen)}".Trim(), 2000)
                    };
                    _db.Despliegues.Add(d);
                    await _infra.ActualizarVersionAsync(c.AmbienteId, d);
                }
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            await _auditoria.RegistrarAsync(AccionesAuditoria.DespliegueTerminado,
                detalle: $"#{e.Id} {Etiquetas.Texto(e.Estado)}: {e.Resumen}", entidad: "EjecucionDespliegue", entidadId: e.Id.ToString(), email: e.IniciadoPor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo registrar el resultado del despliegue {Id}.", e.Id);
        }
    }

    // ── GitHub ────────────────────────────────────────────────────────────────

    private HttpRequestMessage GitHub(string ruta)
    {
        var baseUrl = (_config["Integraciones:GitHub:BaseUrl"] ?? "https://api.github.com").TrimEnd('/');
        var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + ruta);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config["Integraciones:GitHub:Token"]);
        req.Headers.UserAgent.ParseAdd("GAS-Admin");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        return req;
    }

    private async Task<(string Sha, string Mensaje)> ResolverCommitAsync(string repo, string referencia, CancellationToken ct)
    {
        using var req = GitHub($"/repos/{repo}/commits/{Uri.EscapeDataString(referencia)}");
        using var resp = await _http.CreateClient("despliegues").SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new Paso(resp.StatusCode switch
            {
                System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.UnprocessableEntity =>
                    $"GitHub no encontró «{referencia}» en {repo} (o el token no tiene acceso al repositorio).",
                System.Net.HttpStatusCode.Unauthorized => "El token de GitHub no es válido o expiró.",
                _ => $"GitHub respondió {(int)resp.StatusCode}."
            });
        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return (json.RootElement.GetProperty("sha").GetString()!,
                json.RootElement.GetProperty("commit").GetProperty("message").GetString() ?? "");
    }

    private async Task<long> DescargarCodigoAsync(string repo, string sha, string destino, CancellationToken ct)
    {
        using var req = GitHub($"/repos/{repo}/tarball/{sha}");
        using var resp = await _http.CreateClient("despliegues").SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) throw new Paso($"No se pudo descargar el código de GitHub ({(int)resp.StatusCode}).");
        await using var archivo = File.Create(destino);
        await resp.Content.CopyToAsync(archivo, ct);
        return archivo.Length;
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    /// <summary>Variables del ambiente más Gas__Secreto (si aplica y no se capturó a mano).</summary>
    private Dictionary<string, string> VariablesFinales(ConfiguracionDespliegue c, int productoId)
    {
        var vars = _despliegues.Variables(c.AmbienteId);
        if (c.IncluirSecretoGas && !vars.ContainsKey("Gas__Secreto"))
        {
            var clave = _db.ConexionesProducto.AsNoTracking().Where(x => x.ProductoId == productoId).Select(x => x.Clave).FirstOrDefault();
            if (clave is not null && _config[$"Productos:Conexiones:{clave}:Secreto"] is { Length: > 0 } secreto)
                vars["Gas__Secreto"] = secreto;
        }
        foreach (var (k, v) in vars)
            if (v.Contains('\n') || v.Contains('\r')) throw new Paso($"La variable {k} tiene saltos de línea; Docker no los admite.");
        return vars;
    }

    /// <summary>docker run con la configuración del ambiente; {IMAGEN} se sustituye al revertir.</summary>
    private static string ComandoRun(ConfiguracionDespliegue c, string dir, string imagen)
    {
        var sb = new StringBuilder($"docker run -d --name {Sh.Q(c.Contenedor)} --restart unless-stopped --env-file {dir}/{c.Contenedor}.env");
        if (!string.IsNullOrWhiteSpace(c.Red)) sb.Append(" --network ").Append(Sh.Q(c.Red.Trim()));
        foreach (var p in Lineas(c.Puertos)) sb.Append(" -p ").Append(Sh.Q(p));
        foreach (var v in Lineas(c.Volumenes)) sb.Append(" -v ").Append(Sh.Q(v));
        sb.Append(' ').Append(imagen == "{IMAGEN}" ? imagen : Sh.Q(imagen));
        return sb.ToString();
    }

    private static string ImagenMigraciones(ConfiguracionDespliegue c) => $"{c.Imagen}-migraciones";

    public static IEnumerable<string> Lineas(string? texto) =>
        (texto ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'));

    private async Task Remoto(ClienteSsh ssh, string comando, string paso, CancellationToken ct, bool permitirFalla = false)
    {
        var r = await ssh.EjecutarAsync(comando, Linea, LimiteComando, ct);
        if (!r.Ok && !permitirFalla) throw new Paso($"{paso}: falló (código {r.Codigo}).");
    }

    public static string Slug(string texto)
    {
        var normal = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normal)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
        }
        var s = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return s.Length == 0 ? "producto" : s;
    }

    [GeneratedRegex(@"ANTERIOR=(\S*)")]
    private static partial Regex AnteriorRegex();

    private static string? Recortar(string? v, int max) => v is null ? null : v.Length > max ? v[..max] : v;

    // ── Bitácora ──────────────────────────────────────────────────────────────

    private void Titulo(string texto) => Linea($"── {texto} ──");

    private void Linea(string texto)
    {
        lock (_candado)
        {
            _bitacora.Append(Fechas.ALocal(DateTime.UtcNow).ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append("  ").Append(texto).Append('\n');
            if (_bitacora.Length > MaxBitacora)
            {
                var inicio = _bitacora.ToString(0, 20_000);
                var fin = _bitacora.ToString(_bitacora.Length - 300_000, 300_000);
                _bitacora.Clear().Append(inicio).Append("\n… (se omitió parte de la salida) …\n\n").Append(fin);
            }
            _sucia = true;
        }
    }

    /// <summary>Guarda la bitácora en la base cada 1.5 s (con su propio contexto) para que la pantalla la muestre en vivo.</summary>
    private async Task GuardarBitacoraPeriodicamenteAsync(int id, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GasDbContext>();
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1500, ct);
            string? texto = null;
            lock (_candado)
            {
                if (_sucia) { texto = _bitacora.ToString(); _sucia = false; }
            }
            if (texto is null) continue;
            try
            {
                await db.EjecucionesDespliegue.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Bitacora, texto), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo guardar la bitácora del despliegue {Id}.", id);
            }
        }
    }
}
