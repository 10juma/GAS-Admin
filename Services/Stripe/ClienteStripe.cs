using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace GAS.Admin.Services.Stripe;

/// <summary>
/// Configuración de Stripe (sección "Stripe"). Las API keys van en user-secrets o variables
/// de entorno como Stripe:Claves:{clave}; nunca en la base de datos.
/// </summary>
public class OpcionesStripe
{
    public string BaseUrl { get; set; } = "https://api.stripe.com";
    public Dictionary<string, string> Claves { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool SincronizacionAutomatica { get; set; } = true;
    public int MinutosEntreSincronizaciones { get; set; } = 60;
}

public class ErrorStripe(string mensaje) : Exception(mensaje);

/// <summary>Cliente mínimo de solo lectura para la API REST de Stripe.</summary>
public class ClienteStripe
{
    /// <summary>Versión fija de la API para que los campos no cambien sin aviso.</summary>
    public const string VersionApi = "2024-06-20";

    private static readonly HashSet<string> SinDecimales = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    private readonly HttpClient _http;

    public ClienteStripe(HttpClient http) => _http = http;

    /// <summary>Convierte un importe de Stripe (unidades mínimas) a decimal.</summary>
    public static decimal Importe(long minimo, string moneda) => SinDecimales.Contains(moneda) ? minimo : minimo / 100m;

    public async Task<JsonElement> ObtenerAsync(string apiKey, string ruta, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ruta);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Headers.Add("Stripe-Version", VersionApi);
        using var resp = await _http.SendAsync(req, ct);
        var cuerpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            // El mensaje de Stripe nunca incluye la clave; se recorta por si acaso.
            string mensaje;
            try { mensaje = JsonDocument.Parse(cuerpo).RootElement.GetProperty("error").GetProperty("message").GetString() ?? cuerpo; }
            catch { mensaje = cuerpo; }
            throw new ErrorStripe($"Stripe respondió {(int)resp.StatusCode}: {(mensaje.Length > 300 ? mensaje[..300] : mensaje)}");
        }
        return JsonDocument.Parse(cuerpo).RootElement.Clone();
    }

    /// <summary>Recorre una lista paginada de Stripe (limit=100, starting_after).</summary>
    public async IAsyncEnumerable<JsonElement> ListarAsync(string apiKey, string ruta, [EnumeratorCancellation] CancellationToken ct = default)
    {
        string? despues = null;
        var separador = ruta.Contains('?') ? "&" : "?";
        for (var pagina = 0; pagina < 500; pagina++)
        {
            var url = $"{ruta}{separador}limit=100" + (despues is null ? "" : $"&starting_after={Uri.EscapeDataString(despues)}");
            var lista = await ObtenerAsync(apiKey, url, ct);
            var datos = lista.GetProperty("data");
            foreach (var item in datos.EnumerateArray())
            {
                despues = item.GetProperty("id").GetString();
                yield return item;
            }
            if (!lista.TryGetProperty("has_more", out var mas) || !mas.GetBoolean() || datos.GetArrayLength() == 0) yield break;
        }
    }
}

/// <summary>Lectura segura de campos opcionales del JSON de Stripe.</summary>
public static class Json
{
    public static JsonElement? Prop(this JsonElement e, string nombre) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nombre, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    public static string? Texto(this JsonElement e, string nombre) =>
        e.Prop(nombre) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    public static long Entero(this JsonElement e, string nombre) =>
        e.Prop(nombre) is { ValueKind: JsonValueKind.Number } v ? v.GetInt64() : 0;

    public static bool Booleano(this JsonElement e, string nombre) =>
        e.Prop(nombre) is { ValueKind: JsonValueKind.True };

    public static DateTime? Fecha(this JsonElement e, string nombre) =>
        e.Prop(nombre) is { ValueKind: JsonValueKind.Number } v ? DateTimeOffset.FromUnixTimeSeconds(v.GetInt64()).UtcDateTime : null;

    /// <summary>Id de un campo que puede venir como texto o como objeto expandido.</summary>
    public static string? Id(this JsonElement e, string nombre) => e.Prop(nombre) switch
    {
        { ValueKind: JsonValueKind.String } v => v.GetString(),
        { ValueKind: JsonValueKind.Object } o => o.Texto("id"),
        _ => null
    };
}
