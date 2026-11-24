using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Finanzas;
using GAS.Admin.Services.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Productos;

/// <summary>
/// Configuración de conexiones con productos (sección "Productos"). El secreto de cada producto
/// va en Productos:Conexiones:{clave}:Secreto (user-secrets o variables de entorno), nunca en la BD.
/// </summary>
public class OpcionesConexiones
{
    public bool SincronizacionAutomatica { get; set; } = true;
    public int MinutosEntreSincronizaciones { get; set; } = 15;
    public Dictionary<string, OpcionConexion> Conexiones { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class OpcionConexion
{
    public string? Secreto { get; set; }
}

public class ErrorConexion(string mensaje) : Exception(mensaje);

public sealed record ResultadoConexion(string Producto, bool Exito, string Mensaje);

public sealed record AccionCuenta(string Clave, string Texto, string Icono, string Tono, bool PideDias);

/// <summary>Cliente de la "API GAS v1" que implementa cada producto (docs/api-gas-v1.md).</summary>
public class ServicioConexiones
{
    public const string RutaContrato = "docs/api-gas-v1.md";

    public static readonly AccionCuenta[] Acciones =
    [
        new("suspender", "Suspender cuenta", "lock", "danger", false),
        new("reactivar", "Reactivar cuenta", "unlock", "success", false),
        new("extender-prueba", "Extender prueba", "calendar", "info", true),
        new("reenviar-acceso", "Reenviar acceso", "mail", "info", false),
    ];

    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly GasDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IOptionsMonitor<OpcionesConexiones> _monitorOpciones;
    private OpcionesConexiones _op => _monitorOpciones.CurrentValue; // siempre la configuración vigente
    private readonly ILogger<ServicioConexiones> _logger;

    public ServicioConexiones(GasDbContext db, IHttpClientFactory http, IOptionsMonitor<OpcionesConexiones> op, ILogger<ServicioConexiones> logger)
    {
        _db = db;
        _http = http;
        _monitorOpciones = op;
        _logger = logger;
    }

    /// <summary>Secreto compartido del producto (o null si no está configurado).</summary>
    public byte[]? Secreto(string clave)
    {
        if (!_op.Conexiones.TryGetValue(clave, out var c) || string.IsNullOrWhiteSpace(c.Secreto)) return null;
        try { return Base64Url(c.Secreto.Trim()); }
        catch (FormatException) { return Encoding.UTF8.GetBytes(c.Secreto.Trim()); }
    }

    public bool SecretoConfigurado(string clave) => Secreto(clave) is not null;

    // ===================== Firma =====================

    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

    public static string Firmar(byte[] secreto, long timestamp, string metodo, string rutaConQuery, string cuerpo)
    {
        var hashCuerpo = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(cuerpo)));
        var cadena = $"{timestamp}\n{metodo.ToUpperInvariant()}\n{rutaConQuery}\n{hashCuerpo}";
        return Hex(HMACSHA256.HashData(secreto, Encoding.UTF8.GetBytes(cadena)));
    }

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64Url(string s)
    {
        var t = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(t.PadRight(t.Length + (4 - t.Length % 4) % 4, '='));
    }

    private async Task<(JsonElement Json, int Ms)> EnviarAsync(ConexionProducto c, HttpMethod metodo, string ruta, object? cuerpo, string usuario, CancellationToken ct)
    {
        var secreto = Secreto(c.Clave) ?? throw new ErrorConexion($"Falta el secreto: configúralo como Productos:Conexiones:{c.Clave}:Secreto.");
        var uri = new Uri(c.UrlApi.TrimEnd('/') + ruta);
        var texto = cuerpo is null ? string.Empty : JsonSerializer.Serialize(cuerpo, Json);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var req = new HttpRequestMessage(metodo, uri);
        if (cuerpo is not null) req.Content = new StringContent(texto, Encoding.UTF8, "application/json");
        req.Headers.Add("X-GAS-Timestamp", ts.ToString(CultureInfo.InvariantCulture));
        req.Headers.Add("X-GAS-Firma", Firmar(secreto, ts, metodo.Method, uri.PathAndQuery, texto));
        req.Headers.Add("X-GAS-Usuario", usuario);
        req.Headers.Accept.ParseAdd("application/json");

        var reloj = Stopwatch.StartNew();
        using var resp = await _http.CreateClient("productos").SendAsync(req, ct);
        var respuesta = await resp.Content.ReadAsStringAsync(ct);
        reloj.Stop();

        JsonElement json = default;
        try { json = JsonDocument.Parse(respuesta).RootElement.Clone(); } catch (JsonException) { }
        if (!resp.IsSuccessStatusCode)
        {
            var mensaje = json.ValueKind == JsonValueKind.Object ? json.Texto("mensaje") ?? json.Texto("error") : null;
            throw new ErrorConexion(resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "El producto rechazó la firma: revisa que el secreto sea el mismo en ambos lados y que los relojes estén sincronizados.",
                HttpStatusCode.NotFound when ruta.StartsWith("/cuentas/") => mensaje ?? "La cuenta no existe en el producto.",
                HttpStatusCode.NotFound => $"El producto no tiene {ruta} (¿ya implementa la API GAS v1?).",
                _ => mensaje ?? $"El producto respondió {(int)resp.StatusCode}."
            });
        }
        if (json.ValueKind == JsonValueKind.Undefined) throw new ErrorConexion($"El producto respondió algo que no es JSON en {ruta}.");
        return (json, (int)reloj.ElapsedMilliseconds);
    }

    // ===================== Sincronización =====================

    public async Task<List<ResultadoConexion>> SincronizarTodasAsync(CancellationToken ct = default)
    {
        var ids = await _db.ConexionesProducto.Where(c => c.Activa).Select(c => c.Id).ToListAsync(ct);
        var r = new List<ResultadoConexion>();
        foreach (var id in ids) r.Add(await SincronizarAsync(id, ct));
        return r;
    }

    public async Task<ResultadoConexion> SincronizarAsync(int conexionId, CancellationToken ct = default)
    {
        if (!await Candado.WaitAsync(TimeSpan.FromMinutes(5), ct)) return new("Producto", false, "Ya hay una sincronización en curso.");
        try
        {
            var c = await _db.ConexionesProducto.Include(x => x.Producto).FirstAsync(x => x.Id == conexionId, ct);
            try
            {
                var mensaje = await SincronizarConexionAsync(c, ct);
                c.UltimaSincronizacionUtc = DateTime.UtcNow;
                c.UltimoError = null;
                await _db.SaveChangesAsync(ct);
                return new(c.Producto.Nombre, true, mensaje);
            }
            catch (Exception ex) when (ex is ErrorConexion or HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or UriFormatException or DbUpdateException)
            {
                _logger.LogWarning(ex, "Error al sincronizar la conexión de {Producto}", c.Producto.Nombre);
                _db.ChangeTracker.Clear();
                var con = await _db.ConexionesProducto.FirstAsync(x => x.Id == conexionId, CancellationToken.None);
                con.UltimoError = Recortar(ex is ErrorConexion ? ex.Message : $"No se pudo conectar: {ex.Message}", 1000);
                con.EstadoOk = false;
                await _db.SaveChangesAsync(CancellationToken.None);
                return new(c.Producto.Nombre, false, con.UltimoError!);
            }
        }
        finally
        {
            Candado.Release();
        }
    }

    private async Task<string> SincronizarConexionAsync(ConexionProducto c, CancellationToken ct)
    {
        // ---------- Estado ----------
        var (estado, ms) = await EnviarAsync(c, HttpMethod.Get, "/estado", null, "sistema", ct);
        c.EstadoOk = estado.Booleano("ok");
        c.Version = Recortar(estado.Texto("version"), 50);
        c.Ambiente = Recortar(estado.Texto("ambiente"), 50);
        c.LatenciaMs = ms;
        // Si no cabe completo se omite (un JSON recortado quedaría inválido).
        c.DependenciasJson = estado.Prop("dependencias") is { ValueKind: JsonValueKind.Array } deps && deps.GetRawText().Length <= 4000 ? deps.GetRawText() : null;

        // ---------- Métricas (foto del día) ----------
        var (m, _) = await EnviarAsync(c, HttpMethod.Get, "/metricas", null, "sistema", ct);
        var hoy = ServicioFinanzas.Hoy;
        var foto = await _db.MetricasProducto.FirstOrDefaultAsync(x => x.ConexionProductoId == c.Id && x.Fecha == hoy, ct);
        if (foto is null) _db.MetricasProducto.Add(foto = new MetricaProducto { ConexionProductoId = c.Id, Fecha = hoy });
        foto.CuentasTotales = (int)m.Entero("cuentasTotales");
        foto.CuentasActivas = (int)m.Entero("cuentasActivas");
        foto.CuentasEnPrueba = (int)m.Entero("cuentasEnPrueba");
        foto.CuentasSuspendidas = (int)m.Entero("cuentasSuspendidas");
        foto.UsuariosTotales = (int)m.Entero("usuariosTotales");
        foto.UsuariosActivos24h = (int)m.Entero("usuariosActivos24h");
        foto.UsuariosActivos30d = (int)m.Entero("usuariosActivos30d");
        foto.Altas30d = (int)m.Entero("altas30d");
        foto.Bajas30d = (int)m.Entero("bajas30d");
        foto.AdicionalesJson = m.Prop("adicionales") is { ValueKind: JsonValueKind.Array } ad && ad.GetRawText().Length <= 4000 ? ad.GetRawText() : null;
        foto.ActualizadoUtc = DateTime.UtcNow;

        // ---------- Cuentas (incremental después de la primera vez) ----------
        var desde = c.CuentasSincronizadasUtc?.AddHours(-1);
        var inicio = DateTime.UtcNow;
        var existentes = await _db.CuentasProducto.Where(x => x.ConexionProductoId == c.Id).ToDictionaryAsync(x => x.ExternoId, ct);
        var nuevas = 0;
        var actualizadas = 0;
        for (var pagina = 1; pagina <= 1000; pagina++)
        {
            var ruta = $"/cuentas?pagina={pagina}&porPagina=200" + (desde is null ? "" : $"&modificadasDesde={Uri.EscapeDataString(desde.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}");
            var (lista, _) = await EnviarAsync(c, HttpMethod.Get, ruta, null, "sistema", ct);
            if (lista.Prop("cuentas") is not { ValueKind: JsonValueKind.Array } cuentas) break;
            foreach (var j in cuentas.EnumerateArray())
            {
                var id = j.Texto("id");
                if (id is null) continue;
                if (!existentes.TryGetValue(id, out var cuenta))
                {
                    cuenta = new CuentaProducto { ConexionProductoId = c.Id, ExternoId = Recortar(id, 100)! };
                    _db.CuentasProducto.Add(cuenta);
                    existentes[id] = cuenta;
                    nuevas++;
                }
                else actualizadas++;
                Aplicar(cuenta, j);
            }
            var total = (int)lista.Entero("totalPaginas");
            if (cuentas.GetArrayLength() == 0 || pagina >= total) break;
        }
        await LigarClientesAsync(c, existentes.Values, ct);
        c.CuentasSincronizadasUtc = inicio;
        MarcarSuperAdmin(c);
        await _db.SaveChangesAsync(ct);

        var dependenciasMal = c.DependenciasJson is null ? 0
            : JsonDocument.Parse(c.DependenciasJson).RootElement.EnumerateArray().Count(d => !d.Booleano("ok"));
        return $"v{c.Version ?? "?"} · {(c.EstadoOk == true ? "operando bien" : "con fallas")}" +
               (dependenciasMal > 0 ? $" ({dependenciasMal} dependencia(s) con error)" : "") +
               $" · {ms} ms · {nuevas} cuenta(s) nueva(s), {actualizadas} actualizada(s).";
    }

    private static void Aplicar(CuentaProducto cuenta, JsonElement j)
    {
        cuenta.Nombre = Recortar(j.Texto("nombre"), 200) ?? cuenta.ExternoId;
        cuenta.Email = Recortar(j.Texto("email"), 256);
        cuenta.Plan = Recortar(j.Texto("plan"), 100);
        var estado = (j.Texto("estado") ?? "activa").ToLowerInvariant();
        cuenta.Estado = estado is "activa" or "prueba" or "suspendida" or "cancelada" ? estado : "activa";
        cuenta.CreadaUtc = Fecha(j.Texto("creadaUtc"));
        cuenta.UltimoUsoUtc = Fecha(j.Texto("ultimoUsoUtc"));
        cuenta.PruebaHastaUtc = Fecha(j.Texto("pruebaHastaUtc"));
        cuenta.Usuarios = (int)j.Entero("usuarios");
        cuenta.StripeCustomerId = Recortar(j.Texto("stripeCustomerId"), 100);
        cuenta.ActualizadoUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Si la conexión sincronizó bien y tiene URL de super-admin (acceso firmado), el producto pasa a
    /// "Con super-admin" en el catálogo y toma esa URL si no tenía una. Así no se queda "Por revisar".
    /// </summary>
    private static void MarcarSuperAdmin(ConexionProducto c)
    {
        if (string.IsNullOrWhiteSpace(c.UrlSuperAdmin) || c.Producto is null) return;
        if (c.Producto.SuperAdmin != EstadoSuperAdmin.Existe) c.Producto.SuperAdmin = EstadoSuperAdmin.Existe;
        if (string.IsNullOrWhiteSpace(c.Producto.SuperAdminUrl)) c.Producto.SuperAdminUrl = c.UrlSuperAdmin.Trim();
    }

    /// <summary>Si la cuenta tiene cliente de Stripe y ese suscriptor ya está ligado a un Cliente de GAS, se hereda.</summary>
    private async Task LigarClientesAsync(ConexionProducto c, IEnumerable<CuentaProducto> cuentas, CancellationToken ct)
    {
        var sinCliente = cuentas.Where(x => x.ClienteId is null && x.StripeCustomerId is not null).ToList();
        if (sinCliente.Count == 0) return;
        var ids = sinCliente.Select(x => x.StripeCustomerId!).ToList();
        var ligas = await _db.SuscriptoresStripe.AsNoTracking()
            .Where(s => s.Cuenta.ProductoId == c.ProductoId && s.ClienteId != null && ids.Contains(s.StripeId))
            .Select(s => new { s.StripeId, s.ClienteId }).ToListAsync(ct);
        foreach (var x in sinCliente)
            x.ClienteId = ligas.FirstOrDefault(l => l.StripeId == x.StripeCustomerId)?.ClienteId;
    }

    // ===================== Acciones =====================

    public async Task<(bool Ok, string Mensaje)> EjecutarAccionAsync(int cuentaId, string accion, int? dias, string motivo, string usuario, CancellationToken ct = default)
    {
        var cuenta = await _db.CuentasProducto.Include(x => x.Conexion).ThenInclude(c => c.Producto).FirstOrDefaultAsync(x => x.Id == cuentaId, ct);
        if (cuenta is null) return (false, "La cuenta ya no existe.");
        if (!Acciones.Any(a => a.Clave == accion)) return (false, "Acción no válida.");
        try
        {
            object cuerpo = accion == "extender-prueba" ? new { accion, dias, motivo } : new { accion, motivo };
            var (json, _) = await EnviarAsync(cuenta.Conexion, HttpMethod.Post, $"/cuentas/{Uri.EscapeDataString(cuenta.ExternoId)}/acciones", cuerpo, usuario, ct);
            if (json.Prop("cuenta") is { ValueKind: JsonValueKind.Object } actualizada) Aplicar(cuenta, actualizada);
            await _db.SaveChangesAsync(ct);
            var ok = json.Prop("ok") is null || json.Booleano("ok");
            return (ok, json.Texto("mensaje") ?? (ok ? "Acción aplicada." : "El producto no aplicó la acción."));
        }
        catch (Exception ex) when (ex is ErrorConexion or HttpRequestException or TaskCanceledException)
        {
            return (false, ex is ErrorConexion ? ex.Message : $"No se pudo conectar con {cuenta.Conexion.Producto.Nombre}: {ex.Message}");
        }
    }

    // ===================== Acceso al super-admin =====================

    /// <summary>URL de acceso firmado de un solo uso (válido 60 s), o null si falta configuración.</summary>
    public string? UrlAccesoSuperAdmin(ConexionProducto c, string email, string nombre)
    {
        var secreto = Secreto(c.Clave);
        if (secreto is null || string.IsNullOrWhiteSpace(c.UrlSuperAdmin)) return null;
        var carga = JsonSerializer.Serialize(new
        {
            aud = new Uri(c.UrlSuperAdmin).Host.ToLowerInvariant(),
            email,
            nombre,
            exp = DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds(),
            nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()
        }, Json);
        var parte = Base64Url(Encoding.UTF8.GetBytes(carga));
        var firma = Base64Url(HMACSHA256.HashData(secreto, Encoding.ASCII.GetBytes(parte)));
        return $"{c.UrlSuperAdmin.TrimEnd('/')}/gas/sso?token={parte}.{firma}";
    }

    public static string TextoEstado(string estado) => estado switch
    {
        "activa" => "Activa",
        "prueba" => "En prueba",
        "suspendida" => "Suspendida",
        "cancelada" => "Cancelada",
        _ => estado
    };

    public static string TonoEstado(string estado) => estado switch
    {
        "activa" => "gas-badge--success",
        "prueba" => "gas-badge--info",
        "suspendida" => "gas-badge--danger",
        _ => "gas-badge--plain"
    };

    private static DateTime? Fecha(string? iso) =>
        iso is not null && DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var f) ? f : null;

    private static string? Recortar(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Length > max ? s[..max] : s;
}

/// <summary>Sincroniza las conexiones con productos en segundo plano (por defecto, cada 15 minutos).</summary>
public class SincronizadorConexiones : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<OpcionesConexiones> _monitor;
    private readonly ILogger<SincronizadorConexiones> _logger;
    private OpcionesConexiones _op => _monitor.CurrentValue; // secretos capturados en pantalla, sin reiniciar

    public SincronizadorConexiones(IServiceScopeFactory scopes, IOptionsMonitor<OpcionesConexiones> op, ILogger<SincronizadorConexiones> logger)
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
                    var servicio = scope.ServiceProvider.GetRequiredService<ServicioConexiones>();
                    if (_op.Conexiones.Count > 0)
                    foreach (var r in await servicio.SincronizarTodasAsync(stoppingToken))
                        _logger.LogInformation("Producto {Producto}: {Mensaje}", r.Producto, r.Mensaje);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error en la sincronización automática de productos.");
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
