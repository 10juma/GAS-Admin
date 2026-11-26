using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Correo;
using GAS.Admin.Services.Productos;
using GAS.Admin.Services.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Soporte;

/// <summary>
/// Endpoints externos de soporte (sin sesión, protegidos con firma):
///  - POST /webhooks/resend/correo-entrante — webhook de Resend Inbound (firma Svix).
///  - POST /api/gas/v1/tickets y GET /api/gas/v1/tickets/{folio} — tickets desde los productos (firma HMAC de la API GAS).
///  - POST /api/gas/v1/tickets/{folio}/mensajes y …/estado — el producto agrega mensajes (del cliente o de su panel de
///    soporte) y cambia el estado; la conversación queda igual en ambos lados.
/// </summary>
public static class EndpointsSoporte
{
    private const int MaxCuerpo = 1_000_000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapEndpointsSoporte(this WebApplication app)
    {
        app.MapPost("/webhooks/resend/correo-entrante", CorreoEntranteAsync).DisableAntiforgery();
        app.MapPost("/api/gas/v1/tickets", CrearTicketAsync).DisableAntiforgery();
        app.MapGet("/api/gas/v1/tickets/{folio}", ConsultarTicketAsync);
        app.MapPost("/api/gas/v1/tickets/{folio}/mensajes", MensajeTicketAsync).DisableAntiforgery();
        app.MapPost("/api/gas/v1/tickets/{folio}/estado", EstadoTicketAsync).DisableAntiforgery();
    }

    private static async Task<string?> LeerCuerpoAsync(HttpRequest req)
    {
        if (req.ContentLength is > MaxCuerpo) return null;
        // Se lee con tope aunque no venga Content-Length (cuerpo por partes).
        var buffer = new char[MaxCuerpo + 1];
        using var lector = new StreamReader(req.Body, Encoding.UTF8);
        var total = 0;
        int leidos;
        while (total <= MaxCuerpo && (leidos = await lector.ReadAsync(buffer.AsMemory(total, buffer.Length - total))) > 0) total += leidos;
        return total > MaxCuerpo ? null : new string(buffer, 0, total);
    }

    // ===================== Correo entrante (Resend Inbound) =====================

    /// <summary>Valida la firma Svix: base64(HMAC-SHA256(secreto, id.timestamp.cuerpo)).</summary>
    public static bool FirmaSvixValida(string secreto, string? id, string? timestamp, string? firmas, string cuerpo)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(firmas)) return false;
        if (!long.TryParse(timestamp, out var ts) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300) return false;
        byte[] clave;
        try { clave = Convert.FromBase64String(secreto.StartsWith("whsec_") ? secreto[6..] : secreto); }
        catch (FormatException) { return false; }
        var esperada = HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{cuerpo}"));
        foreach (var parte in firmas.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var valor = parte.StartsWith("v1,") ? parte[3..] : parte;
            try
            {
                if (CryptographicOperations.FixedTimeEquals(esperada, Convert.FromBase64String(valor))) return true;
            }
            catch (FormatException) { }
        }
        return false;
    }

    /// <summary>Primer Reply-To del correo recibido (campo reply_to como texto o lista, o la cabecera Reply-To).</summary>
    public static string? ResponderA(JsonElement correo)
    {
        if (correo.Prop("reply_to") is { } r)
        {
            if (r.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(r.GetString())) return r.GetString();
            if (r.ValueKind == JsonValueKind.Array)
                foreach (var x in r.EnumerateArray())
                    if (x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString())) return x.GetString();
        }
        if (correo.Prop("headers") is { ValueKind: JsonValueKind.Object } h)
            foreach (var c in h.EnumerateObject())
                if (c.Name.Equals("reply-to", StringComparison.OrdinalIgnoreCase) && c.Value.ValueKind == JsonValueKind.String) return c.Value.GetString();
        return null;
    }

    public static bool EsDominioPropio(string email, IEnumerable<string> dominios)
    {
        var arroba = email.LastIndexOf('@');
        if (arroba < 0) return false;
        var dominio = email[(arroba + 1)..].Trim().ToLowerInvariant();
        return dominios.Any(d => dominio == d.ToLowerInvariant() || dominio.EndsWith("." + d.ToLowerInvariant()));
    }

    public static (string Email, string? Nombre) Remitente(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return ("", null);
        var v = valor.Trim();
        var i = v.LastIndexOf('<');
        if (i >= 0 && v.EndsWith('>'))
        {
            var nombre = v[..i].Trim().Trim('"');
            return (v[(i + 1)..^1].Trim(), string.IsNullOrEmpty(nombre) ? null : nombre);
        }
        return (v, null);
    }

    private static async Task<IResult> CorreoEntranteAsync(HttpRequest req, IOptionsMonitor<OpcionesSoporte> op, IOptionsMonitor<OpcionesResend> resend,
        IHttpClientFactory http, ServicioSoporte soporte, ILoggerFactory logs)
    {
        var logger = logs.CreateLogger("Soporte.CorreoEntrante");
        var secreto = op.CurrentValue.CorreoEntrante.SecretoWebhook;
        if (string.IsNullOrWhiteSpace(secreto)) return Results.StatusCode(503);

        var cuerpo = await LeerCuerpoAsync(req);
        if (cuerpo is null) return Results.StatusCode(413);
        if (!FirmaSvixValida(secreto, req.Headers["svix-id"], req.Headers["svix-timestamp"], req.Headers["svix-signature"], cuerpo))
        {
            logger.LogWarning("Webhook de correo entrante con firma inválida.");
            return Results.Unauthorized();
        }

        JsonElement evento;
        try { evento = JsonDocument.Parse(cuerpo).RootElement.Clone(); }
        catch (JsonException) { return Results.BadRequest(); }
        if (evento.Texto("type") != "email.received") return Results.Ok(new { ignorado = true });
        var datos = evento.Prop("data");
        var emailId = datos?.Texto("email_id");
        if (datos is null || emailId is null) return Results.BadRequest();

        // El webhook no trae el cuerpo del correo: se consulta a Resend.
        var asunto = datos.Value.Texto("subject") ?? "(sin asunto)";
        var de = datos.Value.Texto("from");
        var messageId = datos.Value.Texto("message_id");
        string texto = "";
        string? responderA = null;
        if (string.IsNullOrWhiteSpace(resend.CurrentValue.ApiKey)) return Results.StatusCode(503);
        try
        {
            using var r = new HttpRequestMessage(HttpMethod.Get, $"{op.CurrentValue.CorreoEntrante.ResendBaseUrl.TrimEnd('/')}/emails/receiving/{Uri.EscapeDataString(emailId)}");
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resend.CurrentValue.ApiKey);
            using var resp = await http.CreateClient("soporte").SendAsync(r);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Resend respondió {Codigo} al leer el correo {Id}.", (int)resp.StatusCode, emailId);
                return Results.StatusCode(502);
            }
            var correo = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            texto = correo.Texto("text") ?? (correo.Texto("html") is { } html ? ServicioSoporte.TextoDeHtml(html) : "");
            asunto = correo.Texto("subject") ?? asunto;
            de = correo.Texto("from") ?? de;
            messageId = correo.Texto("message_id") ?? messageId;
            responderA = ResponderA(correo);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "No se pudo leer el correo {Id} de Resend.", emailId);
            return Results.StatusCode(502);
        }

        var (email, nombre) = Remitente(de);
        if (string.IsNullOrEmpty(email)) return Results.BadRequest();
        // Formulario del sitio o aviso de un producto: el remitente es nuestro y quien escribió va en Reply-To.
        if (responderA is not null && EsDominioPropio(email, op.CurrentValue.CorreoEntrante.DominiosPropios)
            && Remitente(responderA) is { Email.Length: > 0 } quien && !EsDominioPropio(quien.Email, op.CurrentValue.CorreoEntrante.DominiosPropios))
        {
            logger.LogInformation("Correo de {Propio} registrado a nombre de su Reply-To {Email}.", email, quien.Email);
            (email, nombre) = (quien.Email, quien.Nombre ?? nombre);
        }
        var r2 = await soporte.RecibirCorreoAsync(email, nombre, asunto, texto, messageId ?? emailId);
        if (r2 is null) return Results.Ok(new { repetido = true });
        logger.LogInformation("Correo de {Email} agregado a {Folio} ({Tipo}).", email, r2.Value.Ticket.Folio, r2.Value.Nuevo ? "nuevo" : "hilo");
        return Results.Ok(new { folio = r2.Value.Ticket.Folio, nuevo = r2.Value.Nuevo });
    }

    // ===================== Tickets desde los productos (API GAS) =====================

    private sealed record TicketProducto(string? CuentaId, string? Nombre, string? Email, string? Asunto, string? Descripcion,
        string? Prioridad, string? Categoria, string? Referencia);

    /// <summary>Valida la firma HMAC de la API GAS con el secreto del producto indicado en X-GAS-Producto.</summary>
    private static async Task<ConexionProducto?> ConexionFirmadaAsync(HttpRequest req, string cuerpo, GasDbContext db, ServicioConexiones conexiones)
    {
        var clave = req.Headers["X-GAS-Producto"].ToString().Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(clave)) return null;
        var conexion = await db.ConexionesProducto.Include(c => c.Producto).FirstOrDefaultAsync(c => c.Clave == clave && c.Activa);
        var secreto = conexion is null ? null : conexiones.Secreto(clave);
        if (secreto is null) return null;
        if (!long.TryParse(req.Headers["X-GAS-Timestamp"], out var ts) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300) return null;
        var esperada = ServicioConexiones.Firmar(secreto, ts, req.Method, $"{req.Path}{req.QueryString}", cuerpo);
        var recibida = req.Headers["X-GAS-Firma"].ToString();
        return recibida.Length == esperada.Length
               && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(esperada), Encoding.ASCII.GetBytes(recibida))
            ? conexion : null;
    }

    private static async Task<IResult> CrearTicketAsync(HttpRequest req, GasDbContext db, ServicioConexiones conexiones, ServicioSoporte soporte)
    {
        var cuerpo = await LeerCuerpoAsync(req);
        if (cuerpo is null) return Results.StatusCode(413);
        var conexion = await ConexionFirmadaAsync(req, cuerpo, db, conexiones);
        if (conexion is null) return Results.Json(new { error = "firma inválida" }, statusCode: 401);

        TicketProducto? d;
        try { d = JsonSerializer.Deserialize<TicketProducto>(cuerpo, Json); }
        catch (JsonException) { return Results.Json(new { error = "JSON no válido" }, statusCode: 400); }
        if (d is null || string.IsNullOrWhiteSpace(d.Asunto) || string.IsNullOrWhiteSpace(d.Descripcion))
            return Results.Json(new { error = "asunto y descripcion son obligatorios" }, statusCode: 422);
        if (d.Referencia is { Length: > 100 })
            return Results.Json(new { error = "referencia no puede exceder 100 caracteres" }, statusCode: 422);

        // Si ya existe un ticket con la misma referencia del producto, se devuelve ese (idempotente).
        if (!string.IsNullOrWhiteSpace(d.Referencia))
        {
            var refExt = $"{conexion.Clave}:{d.Referencia}";
            var previo = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.ReferenciaExterna == refExt);
            if (previo is not null) return Results.Json(new { folio = previo.Folio, id = previo.Id, existente = true });
        }

        var cuenta = d.CuentaId is null ? null
            : await db.CuentasProducto.AsNoTracking().FirstOrDefaultAsync(c => c.ConexionProductoId == conexion.Id && c.ExternoId == d.CuentaId);
        var prioridad = Enum.TryParse<Prioridad>(d.Prioridad, true, out var p) && Enum.IsDefined(p) && !int.TryParse(d.Prioridad, out _) ? p : Prioridad.Media;
        var categoria = d.Categoria?.ToLowerInvariant() switch
        {
            "problema" or "error" => CategoriaTicket.Problema,
            "solicitud" => CategoriaTicket.Solicitud,
            "facturacion" or "facturación" or "pagos" => CategoriaTicket.Facturacion,
            "sugerencia" => CategoriaTicket.Sugerencia,
            _ => CategoriaTicket.Duda
        };
        var email = d.Email?.Trim();
        var t = await soporte.CrearAsync(new DatosTicketNuevo(
            d.Asunto.Trim(), d.Descripcion.Trim(), CanalTicket.Producto, prioridad, categoria,
            conexion.ProductoId, cuenta?.ClienteId, cuenta?.Id, d.Nombre ?? cuenta?.Nombre, email ?? cuenta?.Email, null,
            d.Nombre ?? email ?? "Usuario del producto", email,
            string.IsNullOrWhiteSpace(d.Referencia) ? null : $"{conexion.Clave}:{d.Referencia}"));
        return Results.Json(new { folio = t.Folio, id = t.Id }, statusCode: 201);
    }

    private static async Task<IResult> ConsultarTicketAsync(string folio, HttpRequest req, GasDbContext db, ServicioConexiones conexiones)
    {
        var conexion = await ConexionFirmadaAsync(req, string.Empty, db, conexiones);
        if (conexion is null) return Results.Json(new { error = "firma inválida" }, statusCode: 401);
        var t = await db.Tickets.AsNoTracking().Include(x => x.Mensajes)
            .FirstOrDefaultAsync(x => x.Folio == folio && x.ProductoId == conexion.ProductoId);
        if (t is null) return Results.Json(new { error = "no encontrado" }, statusCode: 404);
        var prefijo = conexion.Clave + ":";
        return Results.Json(new
        {
            folio = t.Folio,
            asunto = t.Asunto,
            estado = EstadoApi(t.Estado),
            actualizadoUtc = Fecha(t.UltimaActividadUtc),
            // Solo lo que vio el cliente: nunca notas internas. "referencia" = id del mensaje en el producto si salió de ahí.
            mensajes = t.Mensajes.Where(m => m.Tipo is TipoMensajeTicket.Cliente or TipoMensajeTicket.Respuesta).OrderBy(m => m.FechaUtc)
                .Select(m => new
                {
                    id = m.Id,
                    tipo = m.Tipo == TipoMensajeTicket.Cliente ? "cliente" : "soporte",
                    autor = m.Autor,
                    texto = m.Texto,
                    fechaUtc = Fecha(m.FechaUtc),
                    referencia = m.ReferenciaExterna is { } r && r.StartsWith(prefijo, StringComparison.Ordinal) ? r[prefijo.Length..] : null
                })
        }, Json);
    }

    private sealed record MensajeProducto(string? Tipo, string? Texto, string? Autor, string? Email, string? Referencia, bool? Resolver);

    /// <summary>
    /// El producto agrega un mensaje: "cliente" (lo escribió el usuario en el producto) o "soporte" (lo escribió el
    /// equipo en el panel del producto). Idempotente por referencia (id del mensaje en el producto).
    /// </summary>
    private static async Task<IResult> MensajeTicketAsync(string folio, HttpRequest req, GasDbContext db, ServicioConexiones conexiones, ServicioSoporte soporte)
    {
        var cuerpo = await LeerCuerpoAsync(req);
        if (cuerpo is null) return Results.StatusCode(413);
        var conexion = await ConexionFirmadaAsync(req, cuerpo, db, conexiones);
        if (conexion is null) return Results.Json(new { error = "firma inválida" }, statusCode: 401);

        MensajeProducto? d;
        try { d = JsonSerializer.Deserialize<MensajeProducto>(cuerpo, Json); }
        catch (JsonException) { return Results.Json(new { error = "JSON no válido" }, statusCode: 400); }
        var tipo = d?.Tipo?.Trim().ToLowerInvariant();
        if (d is null || tipo is not ("cliente" or "soporte") || string.IsNullOrWhiteSpace(d.Texto) || string.IsNullOrWhiteSpace(d.Referencia))
            return Results.Json(new { error = "tipo (cliente|soporte), texto y referencia son obligatorios" }, statusCode: 422);
        if (d.Texto.Length > 8000 || d.Referencia.Length > 100)
            return Results.Json(new { error = "texto (máx. 8000) o referencia (máx. 100) demasiado largos" }, statusCode: 422);

        var t = await db.Tickets.FirstOrDefaultAsync(x => x.Folio == folio && x.ProductoId == conexion.ProductoId);
        if (t is null) return Results.Json(new { error = "no encontrado" }, statusCode: 404);
        var (m, existente, error) = await soporte.MensajeDesdeProductoAsync(t, tipo == "soporte", d.Texto, d.Autor ?? "", d.Email,
            $"{conexion.Clave}:{d.Referencia.Trim()}", d.Resolver == true);
        if (m is null) return Results.Json(new { error }, statusCode: 409);
        return Results.Json(new { id = m.Id, folio = t.Folio, estado = EstadoApi(t.Estado), existente }, Json, statusCode: existente ? 200 : 201);
    }

    private sealed record EstadoProducto(string? Estado);

    /// <summary>El producto cambia el estado (su panel de soporte resolvió, cerró o reabrió el ticket).</summary>
    private static async Task<IResult> EstadoTicketAsync(string folio, HttpRequest req, GasDbContext db, ServicioConexiones conexiones, ServicioSoporte soporte)
    {
        var cuerpo = await LeerCuerpoAsync(req);
        if (cuerpo is null) return Results.StatusCode(413);
        var conexion = await ConexionFirmadaAsync(req, cuerpo, db, conexiones);
        if (conexion is null) return Results.Json(new { error = "firma inválida" }, statusCode: 401);

        EstadoProducto? d;
        try { d = JsonSerializer.Deserialize<EstadoProducto>(cuerpo, Json); }
        catch (JsonException) { return Results.Json(new { error = "JSON no válido" }, statusCode: 400); }
        EstadoTicket? nuevo = d?.Estado?.Trim().ToLowerInvariant() switch
        {
            "abierto" => EstadoTicket.Abierto,
            "resuelto" => EstadoTicket.Resuelto,
            "cerrado" => EstadoTicket.Cerrado,
            _ => null
        };
        if (nuevo is null) return Results.Json(new { error = "estado debe ser abierto, resuelto o cerrado" }, statusCode: 422);

        var t = await db.Tickets.FirstOrDefaultAsync(x => x.Folio == folio && x.ProductoId == conexion.ProductoId);
        if (t is null) return Results.Json(new { error = "no encontrado" }, statusCode: 404);
        if (t.Estado != nuevo)
        {
            var antes = ServicioSoporte.TextoEstado(t.Estado);
            t.Estado = nuevo.Value;
            t.ResueltoUtc = nuevo is EstadoTicket.Resuelto or EstadoTicket.Cerrado ? t.ResueltoUtc ?? DateTime.UtcNow : null;
            t.UltimaActividadUtc = DateTime.UtcNow;
            await soporte.NotaAsync(t, $"{conexion.Producto.Nombre} cambió el estado: {antes} → {ServicioSoporte.TextoEstado(nuevo.Value)}.", "Sistema", TipoMensajeTicket.Sistema);
        }
        return Results.Json(new { folio = t.Folio, estado = EstadoApi(t.Estado) }, Json);
    }

    /// <summary>Estado para los productos: nuevo, abierto, enespera, escalado, resuelto o cerrado.</summary>
    private static string EstadoApi(EstadoTicket e) => e.ToString().ToLowerInvariant();

    private static string Fecha(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
