using System.Net;
using GAS.Admin.Data.Context;
using GAS.Admin.Services.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Correo;

/// <summary>Fila del recuadro de datos (folio, total, vigencia…).</summary>
public sealed record DatoCorreo(string Etiqueta, string Valor, bool Destacado = false);

/// <summary>Botón principal del correo.</summary>
public sealed record BotonCorreo(string Texto, string Url);

/// <summary>Contenido de un correo de GAS-Admin; la plantilla pone marca, estilos y pie.</summary>
public sealed record ContenidoCorreo(
    string Titulo,
    string Etiqueta,
    string? Resumen = null,
    string? MensajeTexto = null,
    string? CuerpoHtml = null,
    IReadOnlyList<DatoCorreo>? Datos = null,
    BotonCorreo? Boton = null,
    string? Firma = null,
    string? Nota = null);

/// <summary>
/// Plantilla única de correos con la identidad visual de GAS-Admin (paleta, tarjeta, tipografía y logo).
/// Todo va en estilos en línea y tablas para que se vea igual en Gmail, Outlook y Apple Mail.
/// </summary>
public class PlantillaCorreo
{
    // Paleta de GAS-Admin (gas.css)
    private const string Fondo = "#f5f6f3", Superficie = "#ffffff", Borde = "#e4e7e1", Oscuro = "#20251f",
        Texto = "#292e28", Tenue = "#6f756c", Sutil = "#9aa097", Verde = "#647d50", VerdeFuerte = "#4f6640", VerdeClaro = "#edf2e8",
        Fuente = "Inter,-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif";

    private readonly GasDbContext _db;
    private readonly IOptionsMonitor<OpcionesSeguridad> _seguridad;

    public PlantillaCorreo(GasDbContext db, IOptionsMonitor<OpcionesSeguridad> seguridad)
    {
        _db = db;
        _seguridad = seguridad;
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Convierte texto escrito por una persona en párrafos (doble salto = párrafo nuevo).</summary>
    public static string Parrafos(string texto) =>
        string.Join("", texto.Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => $"<p style=\"margin:0 0 14px;line-height:1.6;\">{H(p.Trim()).Replace("\n", "<br>")}</p>"));

    public async Task<string> HtmlAsync(ContenidoCorreo c)
    {
        var empresa = await _db.Empresas.AsNoTracking().OrderBy(e => e.Id).FirstOrDefaultAsync();
        var nombre = string.IsNullOrWhiteSpace(empresa?.NombreComercial) ? "Global App Suite" : empresa!.NombreComercial;
        var lema = empresa?.Lema;
        var contacto = new[] { empresa?.Correo, empresa?.Telefono, empresa?.SitioWeb }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        // El logo solo se puede mostrar desde una dirección pública (los clientes de correo no ven localhost).
        var publica = _seguridad.CurrentValue.UrlPublica;
        var logo = Uri.TryCreate(publica, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
            ? $"<img src=\"{H(u.GetLeftPart(UriPartial.Authority))}/images/gas-logo-correo.png\" width=\"36\" height=\"36\" alt=\"\" style=\"display:block;border:0;width:36px;height:36px;\">"
            : $"<div style=\"width:36px;height:36px;border-radius:10px;background:{VerdeClaro};border:1px solid #d6e0cc;color:{VerdeFuerte};font-size:12px;font-weight:800;line-height:36px;text-align:center;letter-spacing:.5px;\">GAS</div>";

        var cuerpo = c.CuerpoHtml ?? (c.MensajeTexto is null ? "" : Parrafos(c.MensajeTexto));

        var datos = c.Datos is { Count: > 0 }
            ? $"""
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:6px 0 18px;border:1px solid {Borde};border-radius:10px;border-collapse:separate;background:{Fondo};">
                {string.Join("", c.Datos.Select((d, i) => $"""
                <tr>
                  <td style="padding:{(i == 0 ? 12 : 4)}px 16px {(i == c.Datos.Count - 1 ? 12 : 4)}px;font-size:13px;color:{Tenue};">{H(d.Etiqueta)}</td>
                  <td align="right" style="padding:{(i == 0 ? 12 : 4)}px 16px {(i == c.Datos.Count - 1 ? 12 : 4)}px;font-size:{(d.Destacado ? 16 : 13)}px;font-weight:{(d.Destacado ? 750 : 600)};color:{(d.Destacado ? VerdeFuerte : Oscuro)};">{H(d.Valor)}</td>
                </tr>
                """))}
              </table>
              """
            : "";

        var boton = c.Boton is null ? "" : $"""
            <table role="presentation" cellspacing="0" cellpadding="0" style="margin:8px 0 20px;">
              <tr><td style="border-radius:9px;background:{Oscuro};">
                <a href="{H(c.Boton.Url)}" style="display:inline-block;padding:13px 22px;font-family:{Fuente};font-size:14px;font-weight:650;color:#ffffff;text-decoration:none;border-radius:9px;">{H(c.Boton.Texto)} &rarr;</a>
              </td></tr>
            </table>
            <p style="margin:0 0 16px;font-size:12px;line-height:1.5;color:{Sutil};">Si el botón no funciona, copia esta dirección en tu navegador:<br><a href="{H(c.Boton.Url)}" style="color:{Verde};word-break:break-all;">{H(c.Boton.Url)}</a></p>
            """;

        var firma = c.Firma is null ? "" : $"""
            <table role="presentation" cellspacing="0" cellpadding="0" style="margin-top:6px;"><tr>
              <td style="border-left:3px solid {Verde};padding:2px 0 2px 12px;font-size:13px;line-height:1.5;color:{Tenue};">{H(c.Firma).Replace("\n", "<br>")}</td>
            </tr></table>
            """;

        var nota = c.Nota is null ? "" : $"""
            <tr><td style="padding:14px 28px;border-top:1px solid {Borde};background:{Fondo};border-radius:0 0 14px 14px;font-size:12px;line-height:1.5;color:{Tenue};">{H(c.Nota)}</td></tr>
            """;

        return $"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta name="color-scheme" content="light only">
              <title>{H(c.Titulo)}</title>
            </head>
            <body style="margin:0;padding:0;background:{Fondo};font-family:{Fuente};color:{Texto};-webkit-text-size-adjust:100%;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{H(c.Resumen ?? c.Titulo)}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:{Fondo};">
                <tr><td align="center" style="padding:28px 14px;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:580px;">
                    <tr><td style="padding:0 4px 16px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr>
                        <td width="46" valign="middle">{logo}</td>
                        <td valign="middle">
                          <div style="font-size:12px;font-weight:750;letter-spacing:.9px;color:{Oscuro};">{H(nombre.ToUpperInvariant())}</div>
                          <div style="font-size:9px;letter-spacing:1.6px;color:{Tenue};margin-top:2px;">ADMINISTRATION</div>
                        </td>
                        <td align="right" valign="middle">
                          <span style="display:inline-block;padding:4px 11px;border:1px solid #d6e0cc;border-radius:999px;background:{VerdeClaro};color:{VerdeFuerte};font-size:11px;font-weight:650;">{H(c.Etiqueta)}</span>
                        </td>
                      </tr></table>
                    </td></tr>
                    <tr><td style="background:{Superficie};border:1px solid {Borde};border-radius:14px;box-shadow:0 1px 2px rgba(32,37,31,.04);">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                        <tr><td style="height:4px;background:{Verde};border-radius:14px 14px 0 0;font-size:0;line-height:0;">&nbsp;</td></tr>
                        <tr><td style="padding:26px 28px 10px;">
                          <h1 style="margin:0 0 16px;font-size:20px;line-height:1.3;font-weight:750;color:{Oscuro};">{H(c.Titulo)}</h1>
                          <div style="font-size:14px;color:{Texto};">{cuerpo}</div>
                          {datos}{boton}{firma}
                          <div style="height:16px;line-height:16px;font-size:0;">&nbsp;</div>
                        </td></tr>
                        {nota}
                      </table>
                    </td></tr>
                    <tr><td align="center" style="padding:18px 10px 0;font-size:11px;line-height:1.6;color:{Sutil};">
                      <strong style="color:{Tenue};letter-spacing:.6px;">{H(nombre.ToUpperInvariant())}</strong>{(string.IsNullOrWhiteSpace(lema) ? "" : $" · {H(lema)}")}
                      {(contacto.Count == 0 ? "" : $"<br>{string.Join(" · ", contacto.Select(H))}")}
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
