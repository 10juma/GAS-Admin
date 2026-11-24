using System.Net.Mail;
using System.Text.RegularExpressions;

namespace GAS.Admin.Services.Configuracion;

/// <summary>Un dato que se captura en pantalla. Secreto: solo se escribe; ajuste: se muestra.</summary>
public sealed record CampoLlave(
    string Clave, string Etiqueta, bool EsSecreto, string? Ejemplo = null, string? Ayuda = null,
    Func<string, string?>? Validar = null, bool Generable = false, bool Obligatorio = true);

/// <summary>Grupo de llaves que se edita en un mismo modal.</summary>
public sealed record GrupoLlaves(string Id, string Titulo, string Icono, string Descripcion, IReadOnlyList<CampoLlave> Campos, string? Instrucciones = null);

/// <summary>Todas las llaves que GAS-Admin permite capturar, agrupadas por pantalla.</summary>
public static partial class CatalogoLlaves
{
    // ---------- Configuración de GAS-Admin ----------
    public static readonly GrupoLlaves Correo = new("correo", "Correo saliente (Resend)", "mail",
        "Con esta cuenta GAS-Admin envía cotizaciones, respuestas de soporte y enlaces para recuperar contraseña.",
        [
            new("Resend:ApiKey", "API key", true, "re_…", "En Resend › API Keys crea una llave con permiso de envío (Sending access).",
                v => v.StartsWith("re_") ? null : "Una API key de Resend empieza con re_."),
            new("Resend:From", "Remitente", false, "Global App Suite <no-responder@tudominio.com>",
                "Debe ser de un dominio verificado en Resend.", ValidarRemitente),
        ]);

    public static readonly GrupoLlaves Soporte = new("soporte", "Correo de atención al cliente", "phone",
        "Respuestas de tickets y correo entrante que crea o continúa tickets.",
        [
            new("Soporte:CorreoRespuesta", "Correo para respuestas (Reply-To)", false, "soporte@tudominio.com",
                "Cuando el cliente responde un ticket, su correo llega a esta dirección.", ValidarCorreo, Obligatorio: false),
            new("Soporte:CorreoEntrante:SecretoWebhook", "Secreto del webhook de correo entrante", true, "whsec_…",
                "En Resend › Webhooks, al crear el webhook del evento email.received, copia su Signing secret.",
                v => v.StartsWith("whsec_") ? null : "El secreto del webhook empieza con whsec_.", Obligatorio: false),
        ]);

    public static readonly GrupoLlaves Publica = new("publica", "Dirección pública", "globe",
        "Con ella se arman los enlaces de los correos (recuperar contraseña). En producción es obligatoria.",
        [
            new("Seguridad:UrlPublica", "URL pública de GAS-Admin", false, "https://admin.tudominio.com",
                "Solo el dominio, con https.", ValidarUrlPublica, Obligatorio: false),
        ]);

    // ---------- Integraciones técnicas ----------
    public static readonly GrupoLlaves UptimeRobot = new("uptimerobot", "UptimeRobot", "activity",
        "Disponibilidad de cada ambiente.",
        [new("Integraciones:UptimeRobot:ApiKey", "Read-Only API Key", true, "ur…", "En UptimeRobot › Integrations & API crea una Read-Only API Key.")]);

    public static readonly GrupoLlaves GitHub = new("github", "GitHub", "git",
        "Commits, pull requests y releases.",
        [new("Integraciones:GitHub:Token", "Token", true, "github_pat_…",
            "Fine-grained token de solo lectura a Contents, Metadata y Pull requests de tus repositorios.")]);

    public static readonly GrupoLlaves Sentry = new("sentry", "Sentry", "bug",
        "Errores sin resolver por producto.",
        [
            new("Integraciones:Sentry:Token", "Auth token", true, "sntrys_…", "En Sentry › Settings › Auth Tokens, con permisos project:read y event:read."),
            new("Integraciones:Sentry:Organizacion", "Organización (slug)", false, "tu-organizacion",
                "Aparece en la URL de Sentry: sentry.io/organizations/<slug>/.",
                v => SlugRegex().IsMatch(v) ? null : "Usa solo minúsculas, números y guiones."),
        ]);

    // ---------- Por producto ----------
    public static GrupoLlaves Stripe(string clave, string producto) => new($"stripe:{clave}", $"Stripe · {producto}", "card",
        "Llave para leer suscriptores, suscripciones y facturas de este producto.",
        [new($"Stripe:Claves:{clave}", "API key restringida", true, "rk_live_…",
            "En Stripe crea una clave restringida con permiso de solo lectura para Customers, Subscriptions, Invoices, Charges y Balance.",
            v => v.StartsWith("rk_") || v.StartsWith("sk_") ? null : "Una llave de Stripe empieza con rk_ (restringida) o sk_.")]);

    public static GrupoLlaves Producto(string clave, string producto) => new($"producto:{clave}", $"Secreto compartido · {producto}", "key",
        "Firma las llamadas entre GAS-Admin y el super-admin del producto (API GAS v1).",
        [new($"Productos:Conexiones:{clave}:Secreto", "Secreto compartido", true, null,
            "Genera uno nuevo, guárdalo aquí; el producto lo recibe como variable Gas__Secreto (el despliegue automático la agrega sola).",
            v => v.Length >= 32 ? null : "Usa un secreto de al menos 32 caracteres (el botón Generar crea uno seguro).", Generable: true)],
        "Al reemplazar el secreto, actualízalo también en el producto o las llamadas fallarán.");

    public static readonly IReadOnlyList<GrupoLlaves> DeGasAdmin = [Correo, Soporte, Publica];
    public static readonly IReadOnlyList<GrupoLlaves> DeIntegraciones = [UptimeRobot, GitHub, Sentry];

    public static GrupoLlaves? Fijo(string id) => DeGasAdmin.Concat(DeIntegraciones).FirstOrDefault(g => g.Id == id);

    private static string? ValidarCorreo(string v) => MailAddress.TryCreate(v, out var m) && m.Address == v ? null : "Escribe un correo válido.";

    private static string? ValidarRemitente(string v) => MailAddress.TryCreate(v, out _) ? null : "Usa un correo o el formato Nombre <correo@dominio>.";

    private static string? ValidarUrlPublica(string v) =>
        Uri.TryCreate(v, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.AbsolutePath == "/" && string.IsNullOrEmpty(u.Query)
            ? null : "Escribe solo la dirección con https, por ejemplo https://admin.tudominio.com.";

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,49}$")]
    private static partial Regex SlugRegex();
}
