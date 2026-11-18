using System.Net;
using System.Text;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Correo;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Seguridad;

/// <summary>Configuración de acceso. <c>Seguridad:UrlPublica</c> es la dirección con la que se arman los enlaces de los correos.</summary>
public class OpcionesSeguridad
{
    /// <summary>Ej. https://admin.globalappsuite.com.mx. Obligatoria fuera de Development.</summary>
    public string? UrlPublica { get; set; }
}

/// <summary>
/// "¿Olvidaste tu contraseña?": genera un enlace de un solo uso (30 min) y lo envía por correo.
/// Nunca revela si el correo existe; limita las solicitudes por correo y por IP.
/// </summary>
public class RecuperacionPassword
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EsperaPorCorreo = TimeSpan.FromMinutes(2);
    private const int MaximoPorIpHora = 10;

    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly IServicioCorreo _correo;
    private readonly IAuditoria _auditoria;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _ambiente;
    private readonly OpcionesSeguridad _opciones;
    private readonly ILogger<RecuperacionPassword> _logger;
    private readonly PlantillaCorreo _plantilla;

    public RecuperacionPassword(UserManager<ApplicationUser> usuarios, IServicioCorreo correo, IAuditoria auditoria,
        IMemoryCache cache, IWebHostEnvironment ambiente, IOptionsMonitor<OpcionesSeguridad> opciones, ILogger<RecuperacionPassword> logger,
        PlantillaCorreo plantilla)
    {
        _usuarios = usuarios;
        _correo = correo;
        _auditoria = auditoria;
        _cache = cache;
        _ambiente = ambiente;
        _opciones = opciones.CurrentValue;
        _logger = logger;
        _plantilla = plantilla;
    }

    /// <summary>Hay correo configurado y una dirección pública válida para armar el enlace.</summary>
    public bool Disponible => _ambiente.IsDevelopment() || (_correo.Configurado && UrlPublicaValida is not null);

    private string? UrlPublicaValida =>
        Uri.TryCreate(_opciones.UrlPublica, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
            ? u.GetLeftPart(UriPartial.Authority) : null;

    /// <summary>
    /// Procesa la solicitud. Devuelve false solo si se excedió el límite por IP (para avisar que espere);
    /// en cualquier otro caso la respuesta al usuario es la misma, exista o no el correo.
    /// </summary>
    public async Task<bool> SolicitarAsync(string email, HttpRequest request, Func<string, string, string> armarRuta)
    {
        var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
        var claveIp = $"recuperacion:ip:{ip}";
        var intentosIp = _cache.GetOrCreate(claveIp, e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1); return new int[1]; })!;
        if (Interlocked.Increment(ref intentosIp[0]) > MaximoPorIpHora) return false;

        email = email.Trim();
        var claveCorreo = $"recuperacion:correo:{email.ToLowerInvariant()}";
        if (_cache.TryGetValue(claveCorreo, out _)) return true; // ya se envió uno hace poco: no se reenvía
        _cache.Set(claveCorreo, true, EsperaPorCorreo);

        var usuario = await _usuarios.FindByEmailAsync(email);
        if (usuario is null || !usuario.Activo)
        {
            await _auditoria.RegistrarAsync(AccionesAuditoria.RecuperacionSolicitada,
                detalle: usuario is null ? "Correo no registrado; no se envió nada" : "Cuenta desactivada; no se envió nada",
                usuario: usuario, email: email);
            return true;
        }

        var baseUrl = UrlPublicaValida ?? (_ambiente.IsDevelopment() ? $"{request.Scheme}://{request.Host}" : null);
        if (_ambiente.IsDevelopment() && !_correo.Configurado && baseUrl is not null)
        {
            // Solo en Development y sin Resend: el enlace va a la consola para poder probar el flujo.
            var tokenDev = await _usuarios.GeneratePasswordResetTokenAsync(usuario);
            var enlaceDev = baseUrl + armarRuta(usuario.Id, WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(tokenDev)));
            _logger.LogWarning("[Development] Enlace de recuperación (Resend no configurado): {Enlace}", enlaceDev);
            await _auditoria.RegistrarAsync(AccionesAuditoria.RecuperacionSolicitada,
                detalle: "Development: enlace escrito en la consola (correo no configurado)", usuario: usuario, email: email);
            return true;
        }
        if (!_correo.Configurado || baseUrl is null)
        {
            _logger.LogWarning("Recuperación de contraseña sin enviar: falta Resend o Seguridad:UrlPublica.");
            await _auditoria.RegistrarAsync(AccionesAuditoria.RecuperacionSolicitada,
                detalle: "No se envió: el correo o Seguridad:UrlPublica no están configurados", usuario: usuario, email: email);
            return true;
        }

        var token = await _usuarios.GeneratePasswordResetTokenAsync(usuario);
        var codigo = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var enlace = baseUrl + armarRuta(usuario.Id, codigo);

        var html = await _plantilla.HtmlAsync(new ContenidoCorreo(
            Titulo: "Restablece tu contraseña",
            Etiqueta: "Seguridad",
            Resumen: "Enlace para definir una nueva contraseña de GAS-Admin.",
            MensajeTexto: $"Hola {usuario.NombreVisible},\n\nRecibimos una solicitud para restablecer tu contraseña de GAS-Admin. Usa el botón para definir una nueva.",
            Datos: [new("Cuenta", usuario.Email!), new("El enlace vence en", $"{Vigencia.TotalMinutes:0} minutos")],
            Boton: new("Definir nueva contraseña", enlace),
            Nota: "El enlace solo se puede usar una vez. Si no fuiste tú, ignora este correo: tu contraseña no cambia."));
        var texto = $"Hola {usuario.NombreVisible}, recibimos una solicitud para restablecer tu contraseña de GAS-Admin.\n\n" +
                    $"Define una nueva aquí (vence en {Vigencia.TotalMinutes:0} minutos y es de un solo uso):\n{enlace}\n\n" +
                    "Si no fuiste tú, ignora este correo: tu contraseña no cambia.";

        var resultado = await _correo.EnviarAsync(new MensajeCorreo(
            [usuario.Email!], [], "Restablece tu contraseña de GAS-Admin", html, texto, [], null));

        await _auditoria.RegistrarAsync(AccionesAuditoria.RecuperacionSolicitada,
            detalle: resultado.Exito ? "Enlace enviado por correo" : $"No se pudo enviar el correo: {resultado.Error}",
            usuario: usuario, email: email);
        return true;
    }
}
