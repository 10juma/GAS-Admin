using System.Net;
using System.Text.RegularExpressions;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Correo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Soporte;

/// <summary>
/// Configuración de soporte (sección "Soporte"). El secreto del webhook de correo entrante va en
/// Organización › Configuración (cifrado en la BD) o user-secrets / variables de entorno.
/// </summary>
public class OpcionesSoporte
{
    /// <summary>Dirección a la que el cliente responde (el buzón que recibe Resend Inbound), ej. soporte@…</summary>
    public string? CorreoRespuesta { get; set; }
    public OpcionesCorreoEntrante CorreoEntrante { get; set; } = new();
}

public class OpcionesCorreoEntrante
{
    /// <summary>Secreto de firma del webhook de Resend (whsec_…).</summary>
    public string? SecretoWebhook { get; set; }
    public string ResendBaseUrl { get; set; } = "https://api.resend.com";
}

public sealed record DatosTicketNuevo(
    string Asunto, string Descripcion, CanalTicket Canal, Prioridad Prioridad, CategoriaTicket Categoria,
    int? ProductoId, int? ClienteId, int? CuentaProductoId, string? ContactoNombre, string? ContactoEmail,
    string? AsignadoId, string Autor, string? AutorEmail, string? ReferenciaExterna);

public partial class ServicioSoporte
{
    private readonly GasDbContext _db;
    private readonly IServicioCorreo _correo;
    private readonly PlantillaCorreo _plantilla;
    private readonly OpcionesSoporte _op;
    private readonly ILogger<ServicioSoporte> _logger;

    public ServicioSoporte(GasDbContext db, IServicioCorreo correo, IOptionsMonitor<OpcionesSoporte> op, ILogger<ServicioSoporte> logger, PlantillaCorreo plantilla)
    {
        _db = db;
        _correo = correo;
        _plantilla = plantilla;
        _op = op.CurrentValue;
        _logger = logger;
    }

    public bool CorreoConfigurado => _correo.Configurado;

    [GeneratedRegex(@"\[(TKT-\d{4}-\d{4,})\]", RegexOptions.IgnoreCase)]
    private static partial Regex FolioEnAsunto();

    public async Task<string> SiguienteFolioAsync()
    {
        var prefijo = $"TKT-{Fechas.ALocal(DateTime.UtcNow).Year}-";
        var folios = await _db.Tickets.Where(t => t.Folio.StartsWith(prefijo)).Select(t => t.Folio).ToListAsync();
        var max = folios.Select(f => int.TryParse(f[prefijo.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefijo}{max + 1:0000}";
    }

    /// <summary>Calcula los vencimientos de SLA desde la creación del ticket.</summary>
    public async Task AplicarSlaAsync(Ticket t)
    {
        var sla = await _db.SlaPrioridades.AsNoTracking().FirstOrDefaultAsync(s => s.Prioridad == t.Prioridad);
        var inicio = t.CreadoUtc == default ? DateTime.UtcNow : t.CreadoUtc;
        t.VencePrimeraRespuestaUtc = sla is null ? null : inicio.AddHours(sla.PrimeraRespuestaHoras);
        t.VenceResolucionUtc = sla is null ? null : inicio.AddHours(sla.ResolucionHoras);
    }

    /// <summary>Crea y guarda un ticket con su mensaje inicial.</summary>
    public async Task<Ticket> CrearAsync(DatosTicketNuevo d)
    {
        var t = new Ticket
        {
            Folio = await SiguienteFolioAsync(),
            Asunto = Recortar(d.Asunto, 200) ?? "(sin asunto)",
            Canal = d.Canal,
            Prioridad = d.Prioridad,
            Categoria = d.Categoria,
            ProductoId = d.ProductoId,
            ClienteId = d.ClienteId,
            CuentaProductoId = d.CuentaProductoId,
            ContactoNombre = Recortar(d.ContactoNombre, 200),
            ContactoEmail = Recortar(d.ContactoEmail?.Trim().ToLowerInvariant(), 256),
            AsignadoId = d.AsignadoId,
            Estado = d.AsignadoId is null ? EstadoTicket.Nuevo : EstadoTicket.Abierto,
            ReferenciaExterna = Recortar(d.ReferenciaExterna, 300),
            UltimaActividadUtc = DateTime.UtcNow
        };
        await AplicarSlaAsync(t);
        t.Mensajes.Add(new TicketMensaje
        {
            Tipo = d.Canal == CanalTicket.Interno && d.ContactoEmail is null ? TipoMensajeTicket.Nota : TipoMensajeTicket.Cliente,
            Autor = Recortar(d.Autor, 256) ?? "Cliente",
            AutorEmail = Recortar(d.AutorEmail, 256),
            Texto = string.IsNullOrWhiteSpace(d.Descripcion) ? "(sin descripción)" : d.Descripcion.Trim(),
            CorreoId = d.Canal == CanalTicket.Correo ? Recortar(d.ReferenciaExterna, 200) : null
        });
        _db.Tickets.Add(t);
        // Dos tickets simultáneos pueden calcular el mismo folio: se reintenta con el siguiente.
        await _db.GuardarConFolioAsync(async () => t.Folio = await SiguienteFolioAsync());
        return t;
    }

    /// <summary>Respuesta pública: se guarda en el hilo y se envía por correo al contacto.</summary>
    public async Task<(bool Correo, string? Error)> ResponderAsync(Ticket t, string texto, string autor, string? autorEmail, bool resolver)
    {
        var m = new TicketMensaje { TicketId = t.Id, Tipo = TipoMensajeTicket.Respuesta, Autor = autor, AutorEmail = autorEmail, Texto = texto.Trim() };
        string? error = null;
        if (t.ContactoEmail is null) error = "El ticket no tiene correo de contacto; la respuesta quedó solo en el hilo.";
        else if (!_correo.Configurado) error = "El correo no está configurado (Resend); la respuesta quedó solo en el hilo.";
        else
        {
            var cuerpoTexto = $"{m.Texto}\n\n— {autor}\nGlobal App Suite · Atención al cliente\nTicket {t.Folio}. Responde a este correo para continuar la conversación.";
            var html = await _plantilla.HtmlAsync(new ContenidoCorreo(
                Titulo: t.Asunto,
                Etiqueta: $"Ticket {t.Folio}",
                Resumen: m.Texto.Length > 120 ? m.Texto[..120] + "…" : m.Texto,
                MensajeTexto: m.Texto,
                Firma: $"{autor}\nAtención al cliente",
                Nota: resolver
                    ? $"Marcamos el ticket {t.Folio} como resuelto. Si necesitas algo más, responde a este correo y lo reabrimos."
                    : $"Ticket {t.Folio}. Responde a este correo para continuar la conversación."));
            var r = await _correo.EnviarAsync(new MensajeCorreo([t.ContactoEmail], [], $"[{t.Folio}] {t.Asunto}", html, cuerpoTexto, [], _op.CorreoRespuesta));
            m.CorreoEnviado = r.Exito;
            m.CorreoId = Recortar(r.Id, 200);
            m.CorreoError = Recortar(r.Error, 500);
            if (!r.Exito) error = $"No se pudo enviar el correo: {r.Error}";
        }
        _db.TicketMensajes.Add(m);
        t.PrimeraRespuestaUtc ??= DateTime.UtcNow;
        t.UltimaActividadUtc = DateTime.UtcNow;
        if (resolver)
        {
            t.Estado = EstadoTicket.Resuelto;
            t.ResueltoUtc = DateTime.UtcNow;
        }
        else if (t.Estado is EstadoTicket.Nuevo or EstadoTicket.Abierto) t.Estado = EstadoTicket.EnEspera;
        await _db.SaveChangesAsync();
        return (m.CorreoEnviado == true, error);
    }

    public async Task NotaAsync(Ticket t, string texto, string autor, TipoMensajeTicket tipo = TipoMensajeTicket.Nota)
    {
        _db.TicketMensajes.Add(new TicketMensaje { TicketId = t.Id, Tipo = tipo, Autor = autor, Texto = texto.Trim() });
        t.UltimaActividadUtc = DateTime.UtcNow;
        if (tipo == TipoMensajeTicket.Nota && t.Estado == EstadoTicket.Nuevo) t.Estado = EstadoTicket.Abierto;
        await _db.SaveChangesAsync();
    }

    /// <summary>Crea una tarea en el tablero de Proyectos para que la atienda el equipo técnico.</summary>
    public async Task<Tarea> EscalarAsync(Ticket t, int? trabajoId, int? responsableId, string autor)
    {
        var ultimo = await _db.TicketMensajes.Where(m => m.TicketId == t.Id && m.Tipo == TipoMensajeTicket.Cliente)
            .OrderByDescending(m => m.FechaUtc).Select(m => m.Texto).FirstOrDefaultAsync();
        var tarea = new Tarea
        {
            Titulo = Recortar($"[{t.Folio}] {t.Asunto}", 200)!,
            Descripcion = Recortar($"Escalado desde Atención al cliente ({t.Folio}).\n\n{ultimo}", 4000),
            TrabajoId = trabajoId,
            ResponsableId = responsableId,
            Prioridad = t.Prioridad,
            Estado = EstadoTarea.PorHacer,
            DepartamentoId = await _db.Departamentos.Where(d => d.Nombre == "Desarrollo").Select(d => (int?)d.Id).FirstOrDefaultAsync()
        };
        _db.Tareas.Add(tarea);
        await _db.SaveChangesAsync();
        t.TareaId = tarea.Id;
        t.Estado = EstadoTicket.Escalado;
        await NotaAsync(t, $"Escalado a Proyectos como tarea \"{tarea.Titulo}\".", autor, TipoMensajeTicket.Sistema);
        return tarea;
    }

    // ===================== Correo entrante =====================

    /// <summary>
    /// Agrega un correo recibido al ticket cuyo folio viene en el asunto ([TKT-…]); si no hay
    /// folio o el ticket está cerrado, crea uno nuevo. Ignora correos repetidos (mismo Message-ID).
    /// </summary>
    public async Task<(Ticket Ticket, bool Nuevo)?> RecibirCorreoAsync(string deEmail, string? deNombre, string asunto, string texto, string? messageId)
    {
        deEmail = deEmail.Trim().ToLowerInvariant();
        if (messageId is not null && await _db.TicketMensajes.AnyAsync(m => m.CorreoId == messageId && m.Tipo == TipoMensajeTicket.Cliente))
            return null;
        var limpio = LimpiarRespuesta(texto);
        var folio = FolioEnAsunto().Match(asunto) is { Success: true } mf ? mf.Groups[1].Value.ToUpperInvariant() : null;
        var existente = folio is null ? null : await _db.Tickets.FirstOrDefaultAsync(t => t.Folio == folio);

        // Solo el contacto del ticket puede continuar su hilo; cualquier otro remitente abre uno nuevo
        // (evita que alguien escriba en el ticket de otra persona adivinando el folio).
        var mismoContacto = existente?.ContactoEmail is not null && string.Equals(existente.ContactoEmail, deEmail, StringComparison.OrdinalIgnoreCase);
        if (existente is not null && existente.Estado != EstadoTicket.Cerrado && mismoContacto)
        {
            _db.TicketMensajes.Add(new TicketMensaje
            {
                TicketId = existente.Id, Tipo = TipoMensajeTicket.Cliente, Autor = Recortar(deNombre, 256) ?? deEmail,
                AutorEmail = deEmail, Texto = limpio, CorreoId = Recortar(messageId, 200)
            });
            existente.UltimaActividadUtc = DateTime.UtcNow;
            if (existente.Estado is EstadoTicket.EnEspera or EstadoTicket.Resuelto)
            {
                existente.Estado = EstadoTicket.Abierto;
                existente.ResueltoUtc = null;
            }
            await _db.SaveChangesAsync();
            return (existente, false);
        }

        var cliente = await _db.Clientes.Where(c => c.ContactoEmail != null && c.ContactoEmail.ToLower() == deEmail).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        var cuenta = await _db.CuentasProducto.Where(c => c.Email != null && c.Email.ToLower() == deEmail)
            .Select(c => new { c.Id, c.Conexion.ProductoId, c.ClienteId }).FirstOrDefaultAsync();
        var asuntoLimpio = Regex.Replace(FolioEnAsunto().Replace(asunto, ""), @"^\s*((re|rv|fw|fwd)\s*:\s*)+", "", RegexOptions.IgnoreCase).Trim();
        var nuevo = await CrearAsync(new DatosTicketNuevo(
            string.IsNullOrWhiteSpace(asuntoLimpio) ? "(sin asunto)" : asuntoLimpio, limpio, CanalTicket.Correo, Prioridad.Media, CategoriaTicket.Duda,
            cuenta?.ProductoId, cliente ?? cuenta?.ClienteId, cuenta?.Id, deNombre, deEmail, null, deNombre ?? deEmail, deEmail, messageId));
        if (existente is not null)
            await NotaAsync(nuevo, mismoContacto
                ? $"Llegó una respuesta al ticket cerrado {existente.Folio}; se abrió este ticket nuevo."
                : $"El asunto menciona {existente.Folio}, pero el remitente no es el contacto de ese ticket; se abrió este ticket aparte.",
                "Sistema", TipoMensajeTicket.Sistema);
        return (nuevo, true);
    }

    /// <summary>Quita la parte citada de un correo de respuesta ("El … escribió:", líneas con ">").</summary>
    public static string LimpiarRespuesta(string texto)
    {
        var lineas = (texto ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        var resultado = new List<string>();
        foreach (var l in lineas)
        {
            var t = l.Trim();
            if (Regex.IsMatch(t, @"^(El|On)\s.+(escribió|wrote):?$", RegexOptions.IgnoreCase) || t.StartsWith("-----Original Message") || t.StartsWith("________________"))
                break;
            if (t.StartsWith('>')) continue;
            resultado.Add(l);
        }
        var limpio = string.Join("\n", resultado).Trim();
        return string.IsNullOrEmpty(limpio) ? (texto ?? string.Empty).Trim() : limpio;
    }

    public static string TextoDeHtml(string html)
    {
        var t = Regex.Replace(html, @"<(br|/p|/div|/li)[^>]*>", "\n", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, "<[^>]+>", "");
        return WebUtility.HtmlDecode(t).Trim();
    }

    private static string Html(string texto) =>
        string.Join("", WebUtility.HtmlEncode(texto).Replace("\r\n", "\n").Split("\n\n").Select(p => $"<p>{p.Replace("\n", "<br>")}</p>"));

    public static string? Recortar(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Length > max ? s[..max] : s;

    public static string TextoEstado(EstadoTicket e) => e switch
    {
        EstadoTicket.Nuevo => "Nuevo",
        EstadoTicket.Abierto => "Abierto",
        EstadoTicket.EnEspera => "Esperando al cliente",
        EstadoTicket.Escalado => "Escalado",
        EstadoTicket.Resuelto => "Resuelto",
        _ => "Cerrado"
    };

    public static string TonoEstado(EstadoTicket e) => e switch
    {
        EstadoTicket.Nuevo => "gas-badge--danger",
        EstadoTicket.Abierto => "gas-badge--info",
        EstadoTicket.EnEspera => "gas-badge--warning",
        EstadoTicket.Escalado => "gas-badge--violet",
        EstadoTicket.Resuelto => "gas-badge--success",
        _ => "gas-badge--plain"
    };

    public static string TextoCategoria(CategoriaTicket c) => c switch
    {
        CategoriaTicket.Duda => "Duda",
        CategoriaTicket.Problema => "Problema o error",
        CategoriaTicket.Solicitud => "Solicitud",
        CategoriaTicket.Facturacion => "Facturación y pagos",
        _ => "Sugerencia"
    };

    public static string TextoCanal(CanalTicket c) => c switch
    {
        CanalTicket.Interno => "Captura interna",
        CanalTicket.Producto => "Desde el producto",
        _ => "Correo"
    };

    public static string IconoCanal(CanalTicket c) => c switch
    {
        CanalTicket.Interno => "pencil",
        CanalTicket.Producto => "box",
        _ => "mail"
    };

    public static string Duracion(TimeSpan t) =>
        t.TotalMinutes < 60 ? $"{Math.Max(1, (int)t.TotalMinutes)} min"
        : t.TotalHours < 48 ? $"{t.TotalHours:0.#} h"
        : $"{t.TotalDays:0.#} días";
}
