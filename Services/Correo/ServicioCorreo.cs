using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Correo;

/// <summary>
/// Configuración de Resend. Se captura en Organización › Configuración (cifrada en la BD);
/// también se acepta desde user-secrets o variables de entorno.
/// </summary>
public class OpcionesResend
{
    public string? ApiKey { get; set; }
    /// <summary>Remitente verificado en Resend, p. ej. "Global App Suite &lt;cotizaciones@globalappsuite.com.mx&gt;".</summary>
    public string? From { get; set; }
}

public sealed record AdjuntoCorreo(string Nombre, byte[] Contenido);

public sealed record MensajeCorreo(
    IReadOnlyList<string> Para,
    IReadOnlyList<string> Copia,
    string Asunto,
    string Html,
    string Texto,
    IReadOnlyList<AdjuntoCorreo> Adjuntos,
    string? ResponderA);

public sealed record ResultadoCorreo(bool Exito, string? Id, string? Error);

public interface IServicioCorreo
{
    bool Configurado { get; }
    string? Remitente { get; }
    Task<ResultadoCorreo> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default);
}

/// <summary>Envío de correo con la API HTTP de Resend.</summary>
public class ServicioCorreoResend : IServicioCorreo
{
    private readonly HttpClient _http;
    private readonly OpcionesResend _opciones;
    private readonly ILogger<ServicioCorreoResend> _logger;

    public ServicioCorreoResend(HttpClient http, IOptionsMonitor<OpcionesResend> opciones, ILogger<ServicioCorreoResend> logger)
    {
        _http = http;
        _opciones = opciones.CurrentValue;
        _logger = logger;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_opciones.ApiKey) && !string.IsNullOrWhiteSpace(_opciones.From);
    public string? Remitente => _opciones.From;

    public async Task<ResultadoCorreo> EnviarAsync(MensajeCorreo m, CancellationToken ct = default)
    {
        if (!Configurado)
        {
            return new ResultadoCorreo(false, null, "El envío de correo no está configurado: un Admin debe capturar Resend en Organización › Configuración.");
        }

        var cuerpo = new
        {
            from = _opciones.From,
            to = m.Para,
            cc = m.Copia.Count > 0 ? m.Copia : null,
            reply_to = m.ResponderA,
            subject = m.Asunto,
            html = m.Html,
            text = m.Texto,
            attachments = m.Adjuntos.Select(a => new { filename = a.Nombre, content = Convert.ToBase64String(a.Contenido) })
        };

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(cuerpo, options: new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull })
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opciones.ApiKey);

        try
        {
            using var respuesta = await _http.SendAsync(solicitud, ct);
            var texto = await respuesta.Content.ReadAsStringAsync(ct);
            if (respuesta.IsSuccessStatusCode)
            {
                var id = JsonDocument.Parse(texto).RootElement.TryGetProperty("id", out var p) ? p.GetString() : null;
                return new ResultadoCorreo(true, id, null);
            }

            string? mensaje = null;
            try { mensaje = JsonDocument.Parse(texto).RootElement.GetProperty("message").GetString(); } catch { }
            _logger.LogWarning("Resend respondió {Codigo}: {Mensaje}", (int)respuesta.StatusCode, mensaje ?? texto);
            return new ResultadoCorreo(false, null, $"Resend respondió {(int)respuesta.StatusCode}: {mensaje ?? "error desconocido"}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "No se pudo conectar con Resend.");
            return new ResultadoCorreo(false, null, "No se pudo conectar con el servicio de correo.");
        }
    }
}
