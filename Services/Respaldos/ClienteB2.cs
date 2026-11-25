using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GAS.Admin.Services.Respaldos;

public class ErrorB2(string mensaje, HttpStatusCode? estado = null, string? codigo = null) : Exception(mensaje)
{
    public HttpStatusCode? Estado { get; } = estado;
    public string? Codigo { get; } = codigo;
}

public sealed record ArchivoB2(string FileId, string FileName, long ContentLength);

/// <summary>
/// Cliente mínimo de la API nativa de Backblaze B2 (v2): autorizar, subir con bloqueo (Object Lock),
/// descargar y borrar versiones. Se autoriza una vez por corrida (el token dura 24 h).
/// </summary>
public sealed class ClienteB2
{
    private const string UrlAutorizar = "https://api.backblazeb2.com/b2api/v2/b2_authorize_account";
    private readonly HttpClient _http;
    private string _apiUrl = "", _descargaUrl = "", _token = "", _bucketId = "", _bucket = "";

    public ClienteB2(HttpClient http) => _http = http;

    public string Bucket => _bucket;

    public async Task AutorizarAsync(string idLlave, string llave, string bucket, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, UrlAutorizar);
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{idLlave.Trim()}:{llave.Trim()}")));
        var r = await EnviarAsync<RespuestaAutorizar>(req, ct);
        _apiUrl = r.ApiUrl;
        _descargaUrl = r.DownloadUrl;
        _token = r.AuthorizationToken;
        _bucket = bucket.Trim();

        if (r.Allowed?.BucketId is { Length: > 0 } restringido)
        {
            if (!string.Equals(r.Allowed.BucketName, _bucket, StringComparison.Ordinal))
                throw new ErrorB2($"La llave de B2 solo tiene acceso al bucket «{r.Allowed.BucketName}», no a «{_bucket}».");
            _bucketId = restringido;
            return;
        }
        var lista = await PostAsync<RespuestaBuckets>("b2_list_buckets", new { accountId = r.AccountId, bucketName = _bucket }, ct);
        _bucketId = lista.Buckets.FirstOrDefault()?.BucketId ?? throw new ErrorB2($"No existe el bucket «{_bucket}» o la llave no tiene acceso.");
    }

    /// <summary>
    /// Sube un archivo con bloqueo de gobierno hasta <paramref name="retenerHasta"/>. Si la llave no tiene
    /// permiso para fijar el bloqueo, se sube sin él (aplica la retención por defecto del bucket) y se avisa.
    /// </summary>
    public async Task<(ArchivoB2 Archivo, bool ConBloqueo)> SubirAsync(string nombre, string rutaLocal, DateTime retenerHasta, CancellationToken ct)
    {
        string sha1;
        await using (var f = File.OpenRead(rutaLocal))
            sha1 = Convert.ToHexStringLower(await SHA1.HashDataAsync(f, ct));

        var conBloqueo = true;
        for (var intento = 1; ; intento++)
        {
            var url = await PostAsync<RespuestaUrlSubida>("b2_get_upload_url", new { bucketId = _bucketId }, ct);
            await using var f = File.OpenRead(rutaLocal);
            using var req = new HttpRequestMessage(HttpMethod.Post, url.UploadUrl) { Content = new StreamContent(f) };
            req.Headers.TryAddWithoutValidation("Authorization", url.AuthorizationToken);
            req.Headers.TryAddWithoutValidation("X-Bz-File-Name", CodificarNombre(nombre));
            req.Headers.TryAddWithoutValidation("X-Bz-Content-Sha1", sha1);
            if (conBloqueo)
            {
                req.Headers.TryAddWithoutValidation("X-Bz-File-Retention-Mode", "governance");
                req.Headers.TryAddWithoutValidation("X-Bz-File-Retention-Retain-Until-Timestamp",
                    new DateTimeOffset(DateTime.SpecifyKind(retenerHasta, DateTimeKind.Utc)).ToUnixTimeMilliseconds().ToString());
            }
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            req.Content.Headers.ContentLength = f.Length;
            try
            {
                var a = await EnviarAsync<ArchivoB2>(req, ct);
                return (a, conBloqueo);
            }
            catch (ErrorB2 ex) when (conBloqueo && ex.Estado is HttpStatusCode.Unauthorized && ex.Codigo == "unauthorized")
            {
                // Falta writeFileRetentions: se reintenta sin bloqueo explícito (queda la retención por defecto del bucket).
                conBloqueo = false;
            }
            catch (ErrorB2 ex) when (ex.Estado is HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout or (HttpStatusCode)429
                                     || ex.Codigo is "expired_auth_token" or "bad_auth_token")
            {
                if (intento >= 4) throw;
                await Task.Delay(TimeSpan.FromSeconds(intento * 3), ct);
            }
        }
    }

    /// <summary>Descarga un archivo del bucket a <paramref name="destino"/>.</summary>
    public async Task DescargarAsync(string nombre, Stream destino, CancellationToken ct)
    {
        var url = $"{_descargaUrl}/file/{Uri.EscapeDataString(_bucket)}/{string.Join('/', nombre.Split('/').Select(Uri.EscapeDataString))}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", _token);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) throw await ErrorDeAsync(resp, ct);
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        await s.CopyToAsync(destino, ct);
    }

    /// <summary>Borra una versión. Devuelve false si B2 la tiene bloqueada todavía (Object Lock).</summary>
    public async Task<bool> BorrarAsync(string nombre, string idArchivo, CancellationToken ct)
    {
        try
        {
            await PostAsync<JsonElement>("b2_delete_file_version", new { fileName = nombre, fileId = idArchivo }, ct);
            return true;
        }
        catch (ErrorB2 ex) when (ex.Codigo == "file_not_present")
        {
            return true;
        }
        catch (ErrorB2 ex) when (ex.Estado is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && ex.Codigo is "access_denied" or "unauthorized")
        {
            return false;
        }
    }

    private async Task<T> PostAsync<T>(string operacion, object cuerpo, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/b2api/v2/{operacion}") { Content = JsonContent.Create(cuerpo) };
        req.Headers.TryAddWithoutValidation("Authorization", _token);
        return await EnviarAsync<T>(req, ct);
    }

    private async Task<T> EnviarAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ErrorB2($"No se pudo conectar con Backblaze: {ex.Message}");
        }
        using (resp)
        {
            if (!resp.IsSuccessStatusCode) throw await ErrorDeAsync(resp, ct);
            return await resp.Content.ReadFromJsonAsync<T>(ct) ?? throw new ErrorB2("Backblaze respondió vacío.");
        }
    }

    private static async Task<ErrorB2> ErrorDeAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        string? codigo = null, mensaje = null;
        try
        {
            var e = await resp.Content.ReadFromJsonAsync<RespuestaError>(ct);
            codigo = e?.Code;
            mensaje = e?.Message;
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }
        var texto = resp.StatusCode == HttpStatusCode.Unauthorized && codigo is "bad_auth_token" or "unauthorized" && mensaje is null
            ? "Backblaze rechazó la llave (Key ID o Application Key incorrectos)."
            : $"Backblaze respondió {(int)resp.StatusCode}{(codigo is null ? "" : $" ({codigo})")}: {mensaje ?? resp.ReasonPhrase}";
        return new ErrorB2(texto, resp.StatusCode, codigo);
    }

    /// <summary>B2 pide el nombre codificado como URL, dejando las diagonales.</summary>
    private static string CodificarNombre(string nombre) => string.Join('/', nombre.Split('/').Select(Uri.EscapeDataString));

    private sealed record RespuestaAutorizar(
        [property: JsonPropertyName("accountId")] string AccountId,
        [property: JsonPropertyName("apiUrl")] string ApiUrl,
        [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
        [property: JsonPropertyName("authorizationToken")] string AuthorizationToken,
        [property: JsonPropertyName("allowed")] Permitido? Allowed);

    private sealed record Permitido(
        [property: JsonPropertyName("bucketId")] string? BucketId,
        [property: JsonPropertyName("bucketName")] string? BucketName);

    private sealed record RespuestaBuckets([property: JsonPropertyName("buckets")] List<BucketB2> Buckets);
    private sealed record BucketB2([property: JsonPropertyName("bucketId")] string BucketId);

    private sealed record RespuestaUrlSubida(
        [property: JsonPropertyName("uploadUrl")] string UploadUrl,
        [property: JsonPropertyName("authorizationToken")] string AuthorizationToken);

    private sealed record RespuestaError(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("message")] string? Message);
}
