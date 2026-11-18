using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Infraestructura;
using GAS.Admin.Services.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Integraciones;

/// <summary>
/// Configuración de integraciones (sección "Integraciones"). Las llaves van en user-secrets
/// o variables de entorno (Integraciones__GitHub__Token, etc.); nunca en la base de datos.
/// </summary>
public class OpcionesIntegraciones
{
    public bool SincronizacionAutomatica { get; set; } = true;
    public int MinutosEntreSincronizaciones { get; set; } = 15;
    public OpcionesUptimeRobot UptimeRobot { get; set; } = new();
    public OpcionesGitHub GitHub { get; set; } = new();
    public OpcionesSentry Sentry { get; set; } = new();
}

public class OpcionesUptimeRobot
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.uptimerobot.com";
}

public class OpcionesGitHub
{
    public string? Token { get; set; }
    public string BaseUrl { get; set; } = "https://api.github.com";
}

public class OpcionesSentry
{
    public string? Token { get; set; }
    public string? Organizacion { get; set; }
    public string BaseUrl { get; set; } = "https://sentry.io";
}

public static class Integracion
{
    public const string UptimeRobot = "UptimeRobot";
    public const string GitHub = "GitHub";
    public const string Sentry = "Sentry";
    public static readonly string[] Todas = [UptimeRobot, GitHub, Sentry];
}

public sealed record ResultadoIntegracion(string Integracion, bool Exito, string Mensaje);

public class ErrorIntegracion(string mensaje) : Exception(mensaje);

/// <summary>Sincroniza UptimeRobot, GitHub y Sentry (solo lectura) y aplica sus efectos en Infraestructura.</summary>
public partial class ServicioIntegraciones
{
    private static readonly SemaphoreSlim Candado = new(1, 1);

    private readonly GasDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly ServicioInfraestructura _infra;
    private readonly OpcionesIntegraciones _op;
    private readonly ILogger<ServicioIntegraciones> _logger;

    public ServicioIntegraciones(GasDbContext db, IHttpClientFactory http, ServicioInfraestructura infra,
        IOptionsMonitor<OpcionesIntegraciones> opciones, ILogger<ServicioIntegraciones> logger)
    {
        _db = db;
        _http = http;
        _infra = infra;
        _op = opciones.CurrentValue;
        _logger = logger;
    }

    public bool Configurada(string integracion) => integracion switch
    {
        Integracion.UptimeRobot => !string.IsNullOrWhiteSpace(_op.UptimeRobot.ApiKey),
        Integracion.GitHub => !string.IsNullOrWhiteSpace(_op.GitHub.Token),
        Integracion.Sentry => !string.IsNullOrWhiteSpace(_op.Sentry.Token) && !string.IsNullOrWhiteSpace(_op.Sentry.Organizacion),
        _ => false
    };

    public bool AlgunaConfigurada => Integracion.Todas.Any(Configurada);

    public async Task<List<ResultadoIntegracion>> SincronizarTodasAsync(CancellationToken ct = default)
    {
        var r = new List<ResultadoIntegracion>();
        foreach (var i in Integracion.Todas.Where(Configurada)) r.Add(await SincronizarAsync(i, ct));
        return r;
    }

    public async Task<ResultadoIntegracion> SincronizarAsync(string integracion, CancellationToken ct = default)
    {
        if (!Configurada(integracion)) return new(integracion, false, "No está configurada.");
        if (!await Candado.WaitAsync(TimeSpan.FromMinutes(5), ct)) return new(integracion, false, "Ya hay una sincronización en curso.");
        try
        {
            string mensaje;
            try
            {
                mensaje = integracion switch
                {
                    Integracion.UptimeRobot => await UptimeRobotAsync(ct),
                    Integracion.GitHub => await GitHubAsync(ct),
                    _ => await SentryAsync(ct)
                };
            }
            catch (Exception ex) when (ex is ErrorIntegracion or HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or DbUpdateException)
            {
                _logger.LogWarning(ex, "Error al sincronizar {Integracion}", integracion);
                _db.ChangeTracker.Clear();
                var error = ex is ErrorIntegracion ? ex.Message : $"No se pudo sincronizar: {ex.Message}";
                await GuardarEstadoAsync(integracion, null, error.Length > 1000 ? error[..1000] : error);
                return new(integracion, false, error);
            }
            await GuardarEstadoAsync(integracion, mensaje, null);
            return new(integracion, true, mensaje);
        }
        finally
        {
            Candado.Release();
        }
    }

    private async Task GuardarEstadoAsync(string nombre, string? resultado, string? error)
    {
        var e = await _db.EstadosIntegracion.FirstOrDefaultAsync(x => x.Nombre == nombre);
        if (e is null) _db.EstadosIntegracion.Add(e = new EstadoIntegracion { Nombre = nombre });
        if (error is null)
        {
            e.UltimaSincronizacionUtc = DateTime.UtcNow;
            e.UltimoResultado = resultado;
        }
        e.UltimoError = error;
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    // ===================== HTTP =====================

    private async Task<(JsonElement Json, HttpResponseHeaders Headers)> EnviarAsync(HttpRequestMessage req, string servicio, CancellationToken ct)
    {
        var cliente = _http.CreateClient("integraciones");
        using var resp = await cliente.SendAsync(req, ct);
        var cuerpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detalle = resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "la llave no es válida o expiró",
                HttpStatusCode.Forbidden => "la llave no tiene permiso para este recurso",
                HttpStatusCode.NotFound => "no se encontró (o la llave no tiene acceso)",
                (HttpStatusCode)429 => "se alcanzó el límite de consultas; se reintentará más tarde",
                _ => cuerpo.Length > 200 ? cuerpo[..200] : cuerpo
            };
            throw new ErrorIntegracion($"{servicio} respondió {(int)resp.StatusCode}: {detalle}");
        }
        return (JsonDocument.Parse(cuerpo).RootElement.Clone(), resp.Headers);
    }

    private static Uri Url(string baseUrl, string ruta) => new(new Uri(baseUrl.TrimEnd('/') + "/"), ruta.TrimStart('/'));

    // ===================== UptimeRobot =====================

    private async Task<string> UptimeRobotAsync(CancellationToken ct)
    {
        var op = _op.UptimeRobot;
        var vistos = new List<JsonElement>();
        for (var offset = 0; offset < 5000; offset += 50)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Url(op.BaseUrl, "/v2/getMonitors"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["api_key"] = op.ApiKey!, ["format"] = "json", ["custom_uptime_ratios"] = "30",
                    ["offset"] = offset.ToString(CultureInfo.InvariantCulture), ["limit"] = "50"
                })
            };
            var (json, _) = await EnviarAsync(req, "UptimeRobot", ct);
            if (json.Texto("stat") != "ok")
                throw new ErrorIntegracion($"UptimeRobot: {json.Prop("error")?.Texto("message") ?? "respuesta no válida"}");
            var lista = json.Prop("monitors");
            if (lista is not { ValueKind: JsonValueKind.Array } arr) break;
            vistos.AddRange(arr.EnumerateArray());
            var total = json.Prop("pagination")?.Entero("total") ?? 0;
            if (arr.GetArrayLength() < 50 || offset + 50 >= total) break;
        }

        var existentes = await _db.Monitores.Include(m => m.Incidente).ToDictionaryAsync(m => m.ExternoId, ct);
        var ambientes = await _db.Ambientes.AsNoTracking().Where(a => a.Url != null).Select(a => new { a.Id, a.Url }).ToListAsync(ct);
        var ahora = DateTime.UtcNow;
        int abiertos = 0, resueltos = 0;

        foreach (var j in vistos)
        {
            var id = j.Prop("id") is { } v ? v.ToString() : null;
            if (id is null) continue;
            if (!existentes.TryGetValue(id, out var m))
            {
                m = new MonitorUptime { ExternoId = id };
                _db.Monitores.Add(m);
                existentes[id] = m;
                // Se liga solo al ambiente con la misma dirección.
                var host = Host(j.Texto("url"));
                m.AmbienteId = host is null ? null : ambientes.FirstOrDefault(a => Host(a.Url) == host)?.Id;
            }
            var estado = j.Entero("status") switch
            {
                0 => EstadoMonitor.Pausado,
                2 => EstadoMonitor.Arriba,
                8 => EstadoMonitor.Dudoso,
                9 => EstadoMonitor.Caido,
                _ => EstadoMonitor.Desconocido
            };
            if (estado != m.Estado) m.UltimoCambioUtc = ahora;
            m.Estado = estado;
            m.Nombre = Recortar(j.Texto("friendly_name"), 200) ?? id;
            m.Url = Recortar(j.Texto("url"), 500);
            m.Uptime30d = decimal.TryParse(j.Texto("custom_uptime_ratio"), NumberStyles.Number, CultureInfo.InvariantCulture, out var u) ? Math.Round(u, 3) : null;
            m.Existe = true;
            m.ActualizadoUtc = ahora;

            // Caído → incidente crítico automático; de vuelta arriba → se resuelve solo.
            if (estado == EstadoMonitor.Caido && m.AmbienteId is not null && m.IncidenteId is null)
            {
                var amb = await _db.Ambientes.Include(a => a.Producto).FirstAsync(a => a.Id == m.AmbienteId, ct);
                var inc = new Incidente
                {
                    Folio = await _infra.SiguienteFolioIncidenteAsync(ServicioInfraestructura.Hoy.Year),
                    Titulo = Recortar($"{amb.Nombre} no responde (UptimeRobot)", 200)!,
                    ProductoId = amb.ProductoId,
                    AmbienteId = amb.Id,
                    Severidad = SeveridadIncidente.Critica,
                    Estado = EstadoIncidente.Abierto,
                    InicioUtc = ahora,
                    Impacto = $"Detectado automáticamente: el monitor \"{m.Nombre}\" reporta que {m.Url ?? "el sitio"} no responde."
                };
                _db.Incidentes.Add(inc);
                m.Incidente = inc;  // el enlace se guarda junto con el incidente
                await _infra.SincronizarEstadoAsync(amb.Id, inc);
                await _db.GuardarConFolioAsync(async () => inc.Folio = await _infra.SiguienteFolioIncidenteAsync(ServicioInfraestructura.Hoy.Year), ct);
                abiertos++;
            }
            else if (estado is EstadoMonitor.Arriba or EstadoMonitor.Pausado && m.IncidenteId is not null)
            {
                var inc = m.Incidente ?? await _db.Incidentes.FirstOrDefaultAsync(i => i.Id == m.IncidenteId, ct);
                if (inc is { Abierto: true })
                {
                    inc.Estado = EstadoIncidente.Resuelto;
                    inc.FinUtc = ahora;
                    inc.Acciones = Recortar(string.Join("\n", new[] { inc.Acciones, estado == EstadoMonitor.Pausado
                            ? "Cerrado automáticamente: el monitor de UptimeRobot se pausó (revisa si el servicio quedó bien)."
                            : "Resuelto automáticamente: el monitor de UptimeRobot volvió a responder." }
                        .Where(x => !string.IsNullOrWhiteSpace(x))), 4000);
                    await _infra.SincronizarEstadoAsync(inc.AmbienteId, inc);
                    resueltos++;
                }
                m.IncidenteId = null;
            }
        }
        var ids = vistos.Select(j => j.Prop("id")?.ToString()).ToHashSet();
        foreach (var m in existentes.Values.Where(m => !ids.Contains(m.ExternoId)))
        {
            m.Existe = false;
            // Un monitor borrado en UptimeRobot no debe dejar su incidente abierto para siempre.
            if (m.IncidenteId is not null && await _db.Incidentes.FirstOrDefaultAsync(i => i.Id == m.IncidenteId, ct) is { Abierto: true } inc)
            {
                inc.Estado = EstadoIncidente.Resuelto;
                inc.FinUtc = ahora;
                inc.Acciones = Recortar(string.Join("\n", new[] { inc.Acciones, "Cerrado automáticamente: el monitor se eliminó en UptimeRobot." }.Where(x => !string.IsNullOrWhiteSpace(x))), 4000);
                await _infra.SincronizarEstadoAsync(inc.AmbienteId, inc);
                resueltos++;
            }
            m.IncidenteId = null;
        }
        await _db.SaveChangesAsync(ct);

        var caidos = existentes.Values.Count(m => m.Existe && m.Estado == EstadoMonitor.Caido);
        return $"{vistos.Count} monitores; {caidos} caído(s)"
               + (abiertos > 0 ? $"; {abiertos} incidente(s) abierto(s)" : "")
               + (resueltos > 0 ? $"; {resueltos} incidente(s) resuelto(s)" : "") + ".";
    }

    public static string? Host(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var v = url.Contains("://") ? url : "https://" + url;
        if (!Uri.TryCreate(v, UriKind.Absolute, out var u)) return null;
        var h = u.Host.ToLowerInvariant();
        return h.StartsWith("www.") ? h[4..] : h;
    }

    // ===================== GitHub =====================

    [GeneratedRegex(@"github\.com[/:]([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex RepoGitHubRegex();

    public static string? NombreGitHub(string? url)
    {
        if (url is null || RepoGitHubRegex().Match(url.Trim()) is not { Success: true } m) return null;
        var (duenio, repo) = (m.Groups[1].Value, m.Groups[2].Value);
        // Evita rutas como "../.." que saldrían de /repos/{dueño}/{repo}.
        return duenio.Trim('.').Length == 0 || repo.Trim('.').Length == 0 ? null : $"{duenio}/{repo}";
    }

    private HttpRequestMessage GitHub(string ruta)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Url(_op.GitHub.BaseUrl, ruta));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _op.GitHub.Token);
        req.Headers.UserAgent.ParseAdd("GAS-Admin");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return req;
    }

    private async Task<string> GitHubAsync(CancellationToken ct)
    {
        var repos = await _db.Repositorios.Where(r => !r.Archivado && r.Url != null).ToListAsync(ct);
        var estados = await _db.ReposGitHub.ToDictionaryAsync(r => r.RepositorioId, ct);
        int revisados = 0, conError = 0, despliegues = 0;
        var tocados = new HashSet<int>();

        foreach (var repo in repos)
        {
            var nombre = NombreGitHub(repo.Url);
            if (nombre is null) continue;
            revisados++;
            if (!estados.TryGetValue(repo.Id, out var e))
            {
                e = new RepoGitHub { RepositorioId = repo.Id };
                _db.ReposGitHub.Add(e);
                estados[repo.Id] = e;
            }
            e.NombreCompleto = nombre;
            e.ActualizadoUtc = DateTime.UtcNow;
            try
            {
                using (var req = GitHub($"/repos/{nombre}"))
                {
                    var (r, _) = await EnviarAsync(req, "GitHub", ct);
                    e.RamaPrincipal = r.Texto("default_branch");
                    e.Privado = r.Booleano("private");
                    e.IssuesAbiertos = (int)r.Entero("open_issues_count");
                }
                var rama = e.RamaPrincipal ?? repo.RamaPrincipal;
                using (var req = GitHub($"/repos/{nombre}/commits?sha={Uri.EscapeDataString(rama)}&per_page=1"))
                {
                    var (c, _) = await EnviarAsync(req, "GitHub", ct);
                    if (c.ValueKind == JsonValueKind.Array && c.GetArrayLength() > 0)
                    {
                        var commit = c[0];
                        e.UltimoCommitSha = commit.Texto("sha");
                        var datos = commit.Prop("commit");
                        e.UltimoCommitMensaje = Recortar(datos?.Texto("message")?.Split('\n')[0], 300);
                        e.UltimoCommitAutor = Recortar(commit.Prop("author")?.Texto("login") ?? datos?.Prop("author")?.Texto("name"), 150);
                        e.UltimoCommitUtc = datos?.Prop("author")?.Texto("date") is { } f && DateTime.TryParse(f, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var fecha) ? fecha : null;
                    }
                }
                using (var req = GitHub($"/repos/{nombre}/pulls?state=open&per_page=100"))
                {
                    var (p, _) = await EnviarAsync(req, "GitHub", ct);
                    e.PrsAbiertos = p.ValueKind == JsonValueKind.Array ? p.GetArrayLength() : 0;
                    e.IssuesAbiertos = Math.Max(0, e.IssuesAbiertos - e.PrsAbiertos);
                }
                List<JsonElement> releases;
                using (var req = GitHub($"/repos/{nombre}/releases?per_page=20"))
                {
                    var (rel, _) = await EnviarAsync(req, "GitHub", ct);
                    releases = rel.ValueKind == JsonValueKind.Array
                        ? rel.EnumerateArray().Where(x => !x.Booleano("draft") && !x.Booleano("prerelease") && x.Texto("published_at") is not null)
                            .OrderByDescending(x => Fecha(x.Texto("published_at"))).ToList()
                        : [];
                }
                e.UltimoRelease = Recortar(releases.FirstOrDefault().ValueKind == JsonValueKind.Object ? releases[0].Texto("tag_name") : null, 100);
                e.UltimoReleaseUtc = releases.Count > 0 ? Fecha(releases[0].Texto("published_at")) : null;
                e.Error = null;

                despliegues += await RegistrarReleasesAsync(repo.Id, releases, tocados, ct);
            }
            catch (Exception ex) when (ex is ErrorIntegracion or HttpRequestException or TaskCanceledException or JsonException)
            {
                e.Error = Recortar(ex is ErrorIntegracion ? ex.Message : $"No se pudo leer: {ex.Message}", 500);
                conError++;
            }
        }
        await _db.SaveChangesAsync(ct);
        foreach (var id in tocados) await _infra.ActualizarVersionAsync(id);
        await _db.SaveChangesAsync(ct);

        if (revisados > 0 && conError == revisados)
            throw new ErrorIntegracion($"Ningún repositorio se pudo leer. {estados.Values.First(x => x.Error is not null).Error}");
        return $"{revisados} repositorio(s) revisado(s)" + (conError > 0 ? $", {conError} con error" : "")
               + (despliegues > 0 ? $"; {despliegues} despliegue(s) nuevo(s) desde releases" : "") + ".";
    }

    /// <summary>
    /// Registra los releases nuevos como despliegues en los ambientes de producción del repositorio.
    /// La primera vez solo toma el más reciente, para no llenar la bitácora con historia vieja.
    /// </summary>
    private async Task<int> RegistrarReleasesAsync(int repositorioId, List<JsonElement> releases, HashSet<int> tocados, CancellationToken ct)
    {
        if (releases.Count == 0) return 0;
        var ambientes = await _db.Ambientes.Where(a => a.RepositorioId == repositorioId && a.Tipo == TipoAmbiente.Produccion && a.Estado != EstadoAmbiente.Inactivo)
            .Select(a => a.Id).ToListAsync(ct);
        var nuevos = 0;
        foreach (var ambienteId in ambientes)
        {
            var previos = await _db.Despliegues.Where(d => d.AmbienteId == ambienteId && d.ExternoId != null && d.ExternoId.StartsWith("github-release:"))
                .Select(d => new { d.ExternoId, d.FechaUtc }).ToListAsync(ct);
            var ya = previos.Select(p => p.ExternoId).ToHashSet();
            var desde = previos.Count == 0 ? (DateTime?)null : previos.Max(p => p.FechaUtc);
            var candidatos = previos.Count == 0 ? releases.Take(1) : releases.Where(r => Fecha(r.Texto("published_at")) > desde);
            foreach (var r in candidatos.Reverse())
            {
                var externo = $"github-release:{r.Prop("id")}";
                if (ya.Contains(externo)) continue;
                var notas = r.Texto("name") is { Length: > 0 } n && n != r.Texto("tag_name") ? n : null;
                _db.Despliegues.Add(new Despliegue
                {
                    AmbienteId = ambienteId,
                    Version = Recortar(r.Texto("tag_name"), 50) ?? "release",
                    FechaUtc = Fecha(r.Texto("published_at")) ?? DateTime.UtcNow,
                    Resultado = ResultadoDespliegue.Exitoso,
                    Referencia = Recortar(r.Texto("target_commitish"), 150),
                    Realizo = Recortar($"{r.Prop("author")?.Texto("login") ?? "GitHub"} (release de GitHub)", 256),
                    Notas = Recortar(notas, 2000),
                    ExternoId = externo
                });
                tocados.Add(ambienteId);
                nuevos++;
            }
        }
        return nuevos;
    }

    private static DateTime? Fecha(string? iso) =>
        iso is not null && DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var f) ? f : null;

    // ===================== Sentry =====================

    private HttpRequestMessage Sentry(string ruta)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Url(_op.Sentry.BaseUrl, ruta));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _op.Sentry.Token);
        return req;
    }

    private static int Hits(HttpResponseHeaders h, int respaldo) =>
        h.TryGetValues("X-Hits", out var v) && int.TryParse(v.FirstOrDefault(), out var n) ? n : respaldo;

    private async Task<string> SentryAsync(CancellationToken ct)
    {
        var org = Uri.EscapeDataString(_op.Sentry.Organizacion!);
        JsonElement lista;
        using (var req = Sentry($"/api/0/organizations/{org}/projects/?per_page=100"))
            (lista, _) = await EnviarAsync(req, "Sentry", ct);
        if (lista.ValueKind != JsonValueKind.Array) throw new ErrorIntegracion("Sentry: respuesta no válida al listar proyectos.");

        var proyectos = await _db.ProyectosSentry.ToDictionaryAsync(p => p.ExternoId, ct);
        var productos = await _db.Productos.AsNoTracking().Select(p => new { p.Id, p.Nombre }).ToListAsync(ct);
        int totalSinResolver = 0, totalNuevos = 0;
        var vistos = new HashSet<string>();

        foreach (var j in lista.EnumerateArray())
        {
            var id = j.Texto("id");
            var slug = j.Texto("slug");
            if (id is null || slug is null) continue;
            vistos.Add(id);
            if (!proyectos.TryGetValue(id, out var p))
            {
                p = new ProyectoSentry { ExternoId = id };
                _db.ProyectosSentry.Add(p);
                proyectos[id] = p;
                // Se liga solo al producto cuyo nombre aparece en el proyecto.
                var clave = (slug + " " + j.Texto("name")).Replace("-", "").Replace(" ", "").ToLowerInvariant();
                p.ProductoId = productos.FirstOrDefault(x => clave.Contains(x.Nombre.Replace(" ", "").ToLowerInvariant()))?.Id;
            }
            p.Slug = Recortar(slug, 100)!;
            p.Nombre = Recortar(j.Texto("name"), 200) ?? slug;
            p.Plataforma = Recortar(j.Texto("platform"), 50);
            p.Existe = true;
            p.ActualizadoUtc = DateTime.UtcNow;

            JsonElement issues;
            HttpResponseHeaders cabeceras;
            using (var req = Sentry($"/api/0/projects/{org}/{Uri.EscapeDataString(slug)}/issues/?query={Uri.EscapeDataString("is:unresolved")}&statsPeriod=24h&limit=25"))
                (issues, cabeceras) = await EnviarAsync(req, "Sentry", ct);
            var arreglo = issues.ValueKind == JsonValueKind.Array ? issues.EnumerateArray().ToList() : [];
            p.SinResolver = Hits(cabeceras, arreglo.Count);

            using (var req = Sentry($"/api/0/projects/{org}/{Uri.EscapeDataString(slug)}/issues/?query={Uri.EscapeDataString("is:unresolved firstSeen:-24h")}&limit=1"))
            {
                var (nuevos, h) = await EnviarAsync(req, "Sentry", ct);
                p.Nuevos24h = Hits(h, nuevos.ValueKind == JsonValueKind.Array ? nuevos.GetArrayLength() : 0);
            }
            totalSinResolver += p.SinResolver;
            totalNuevos += p.Nuevos24h;

            if (p.Id != 0) await _db.Entry(p).Collection(x => x.Errores).LoadAsync(ct);
            var porId = p.Errores.ToDictionary(x => x.ExternoId);
            var actuales = new HashSet<string>();
            foreach (var i in arreglo)
            {
                var iid = i.Texto("id");
                if (iid is null) continue;
                actuales.Add(iid);
                if (!porId.TryGetValue(iid, out var err))
                {
                    err = new ErrorSentry { ExternoId = iid };
                    p.Errores.Add(err);
                }
                err.Titulo = Recortar(i.Texto("title"), 500) ?? "(sin título)";
                err.Culpable = Recortar(i.Texto("culprit"), 500);
                err.Nivel = Recortar(i.Texto("level"), 20);
                err.Eventos = int.TryParse(i.Texto("count"), out var n) ? n : (int)i.Entero("count");
                err.Usuarios = (int)i.Entero("userCount");
                err.PrimeraVezUtc = Fecha(i.Texto("firstSeen"));
                err.UltimaVezUtc = Fecha(i.Texto("lastSeen"));
                err.Url = Recortar(i.Texto("permalink"), 500);
                err.ActualizadoUtc = DateTime.UtcNow;
            }
            // Los que ya no aparecen se resolvieron o quedaron fuera de los 25 más recientes.
            foreach (var viejo in p.Errores.Where(x => !actuales.Contains(x.ExternoId)).ToList())
            {
                p.Errores.Remove(viejo);
                if (viejo.Id != 0) _db.ErroresSentry.Remove(viejo);
            }
        }
        foreach (var p in proyectos.Values.Where(p => !vistos.Contains(p.ExternoId)))
        {
            p.Existe = false;
            p.SinResolver = 0;
            p.Nuevos24h = 0;
        }
        await _db.SaveChangesAsync(ct);
        return $"{vistos.Count} proyecto(s); {totalSinResolver} error(es) sin resolver, {totalNuevos} nuevo(s) en 24 h.";
    }

    private static string? Recortar(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Length > max ? s[..max] : s;
}

/// <summary>Sincroniza las integraciones en segundo plano (por defecto, cada 15 minutos).</summary>
public class SincronizadorIntegraciones : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<OpcionesIntegraciones> _monitor;
    private readonly ILogger<SincronizadorIntegraciones> _logger;
    private OpcionesIntegraciones _op => _monitor.CurrentValue; // llaves capturadas en pantalla, sin reiniciar

    public SincronizadorIntegraciones(IServiceScopeFactory scopes, IOptionsMonitor<OpcionesIntegraciones> op, ILogger<SincronizadorIntegraciones> logger)
    {
        _scopes = scopes;
        _monitor = op;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_op.SincronizacionAutomatica) return;
        var intervalo = TimeSpan.FromMinutes(Math.Max(5, _op.MinutosEntreSincronizaciones));
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var servicio = scope.ServiceProvider.GetRequiredService<ServicioIntegraciones>();
                    if (servicio.AlgunaConfigurada)
                        foreach (var r in await servicio.SincronizarTodasAsync(stoppingToken))
                            _logger.LogInformation("{Integracion}: {Mensaje}", r.Integracion, r.Mensaje);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error en la sincronización automática de integraciones.");
                }
                await Task.Delay(intervalo, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // La aplicación se está deteniendo.
        }
    }
}
