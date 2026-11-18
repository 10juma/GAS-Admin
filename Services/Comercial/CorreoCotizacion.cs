using System.Globalization;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Correo;

namespace GAS.Admin.Services.Comercial;

/// <summary>Cuerpo del correo con el que se envía una cotización.</summary>
public static class CorreoCotizacion
{
    private static readonly CultureInfo Mx = new("es-MX");

    public static string AsuntoPorDefecto(Cotizacion c) => $"Cotización {c.FolioCompleto} · {c.Titulo}";

    public static string MensajePorDefecto(Cotizacion c, string? remitente) =>
        $"Hola{(string.IsNullOrWhiteSpace(c.Cliente.ContactoNombre) ? "" : " " + c.Cliente.ContactoNombre.Split(' ')[0])},\n\n" +
        $"Te comparto la cotización {c.FolioCompleto} para \"{c.Titulo}\". La encontrarás adjunta en PDF.\n\n" +
        "Quedo atento a tus comentarios o dudas.\n\n" +
        $"Saludos,\n{remitente ?? "Global App Suite"}";

    /// <summary>Contenido para la plantilla de correo de GAS-Admin.</summary>
    public static ContenidoCorreo Contenido(Cotizacion c, string mensaje) => new(
        Titulo: $"Cotización {c.FolioCompleto}",
        Etiqueta: "Cotización",
        Resumen: $"{c.Titulo} · Total {c.Moneda} {c.Total.ToString("#,##0.00", Mx)}",
        MensajeTexto: mensaje,
        Datos:
        [
            new("Proyecto", c.Titulo),
            new("Folio", c.FolioCompleto),
            new("Vigencia", c.FechaVencimiento.ToString("dd 'de' MMMM 'de' yyyy", Mx)),
            new("Total", $"{c.Moneda} {c.Total.ToString("#,##0.00", Mx)}", Destacado: true),
        ],
        Nota: "La cotización completa va adjunta en PDF. Puedes responder directamente a este correo.");

    public static string Texto(Cotizacion c, string mensaje) =>
        $"{mensaje}\n\nFolio: {c.FolioCompleto}\nTotal: {c.Moneda} {c.Total.ToString("#,##0.00", Mx)}\n" +
        $"Vigencia: {c.FechaVencimiento.ToString("dd/MM/yyyy", Mx)}";
}
