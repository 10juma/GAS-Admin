using System.Globalization;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GAS.Admin.Services.Comercial;

/// <summary>PDF de cotización con la identidad de Global App Suite.</summary>
public class GeneradorPdfCotizacion
{
    private const string Oscuro = "#20251f";
    private const string Verde = "#647d50";
    private const string Tenue = "#6f756c";
    private const string Borde = "#e4e7e1";
    private const string FondoSuave = "#f5f6f3";

    private static readonly CultureInfo Mx = new("es-MX");

    private readonly byte[]? _logo;

    public GeneradorPdfCotizacion(IWebHostEnvironment env)
    {
        var ruta = Path.Combine(env.WebRootPath, "images", "gas-logo.png");
        _logo = File.Exists(ruta) ? File.ReadAllBytes(ruta) : null;
    }

    public static string NombreArchivo(Cotizacion c) => $"{c.FolioCompleto.Replace(' ', '-')}.pdf";

    /// <param name="c">Cotización con Cliente, Responsable y Lineas cargados.</param>
    /// <param name="empresa">Datos de GAS para encabezado y pie (opcional).</param>
    public byte[] Generar(Cotizacion c, Empresa? empresa = null)
    {
        var nombreEmpresa = (empresa?.NombreComercial ?? "Global App Suite").ToUpperInvariant();
        var lema = empresa?.Lema ?? "Software que resuelve problemas reales";
        var pie = new[]
        {
            empresa?.RazonSocial,
            string.IsNullOrWhiteSpace(empresa?.Rfc) ? null : "RFC " + empresa.Rfc,
            string.IsNullOrWhiteSpace(empresa?.Domicilio) ? null : empresa.Domicilio + (string.IsNullOrWhiteSpace(empresa.CodigoPostal) ? "" : ", C.P. " + empresa.CodigoPostal),
            empresa?.Telefono, empresa?.Correo, empresa?.SitioWeb
        }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        string Dinero(decimal v) => $"{c.Moneda} {v.ToString("#,##0.00", Mx)}";
        var cliente = c.Cliente;
        var responsable = c.Responsable;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Oscuro));

                // ---------- Encabezado ----------
                page.Header().PaddingBottom(18).Row(row =>
                {
                    row.RelativeItem().Row(marca =>
                    {
                        if (_logo is not null)
                        {
                            marca.ConstantItem(42).Height(42).Image(_logo).FitArea();
                            marca.ConstantItem(10);
                        }
                        marca.RelativeItem().AlignMiddle().Column(col =>
                        {
                            col.Item().Text(nombreEmpresa).Bold().FontSize(13).LetterSpacing(0.05f);
                            if (!string.IsNullOrWhiteSpace(lema)) col.Item().Text(lema).FontSize(8.5f).FontColor(Tenue);
                        });
                    });

                    row.ConstantItem(200).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text("COTIZACIÓN").Bold().FontSize(15).FontColor(Verde);
                        col.Item().AlignRight().Text(c.FolioCompleto).SemiBold().FontSize(10.5f);
                        col.Item().PaddingTop(4).AlignRight().Text($"Fecha: {c.FechaEmision.ToString("dd 'de' MMMM 'de' yyyy", Mx)}").FontSize(8.5f).FontColor(Tenue);
                        col.Item().AlignRight().Text($"Vigente hasta: {c.FechaVencimiento.ToString("dd 'de' MMMM 'de' yyyy", Mx)}").FontSize(8.5f).FontColor(Tenue);
                    });
                });

                // ---------- Contenido ----------
                page.Content().Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Borde).Background(FondoSuave).Padding(12).Column(b =>
                        {
                            b.Item().Text("CLIENTE").FontSize(7.5f).Bold().FontColor(Tenue).LetterSpacing(0.08f);
                            b.Item().PaddingTop(3).Text(cliente.NombreVisible).Bold().FontSize(11);
                            if (!string.IsNullOrWhiteSpace(cliente.NombreComercial)) b.Item().Text(cliente.Nombre).FontColor(Tenue);
                            if (!string.IsNullOrWhiteSpace(cliente.Rfc)) b.Item().Text($"RFC: {cliente.Rfc}").FontColor(Tenue);
                            if (!string.IsNullOrWhiteSpace(cliente.ContactoNombre)) b.Item().PaddingTop(4).Text($"Atención: {cliente.ContactoNombre}");
                            if (!string.IsNullOrWhiteSpace(cliente.ContactoEmail)) b.Item().Text(cliente.ContactoEmail).FontColor(Tenue);
                        });
                        row.ConstantItem(12);
                        row.RelativeItem().Border(1).BorderColor(Borde).Padding(12).Column(b =>
                        {
                            b.Item().Text("ELABORÓ").FontSize(7.5f).Bold().FontColor(Tenue).LetterSpacing(0.08f);
                            b.Item().PaddingTop(3).Text(responsable?.NombreVisible ?? "Global App Suite").Bold().FontSize(11);
                            if (responsable?.Email is not null) b.Item().Text(responsable.Email).FontColor(Tenue);
                            b.Item().PaddingTop(4).Text($"Moneda: {c.Moneda}");
                        });
                    });

                    col.Item().Text(c.Titulo).Bold().FontSize(13);

                    // Conceptos
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(22);
                            cols.RelativeColumn(5);
                            cols.RelativeColumn(1.4f);
                            cols.RelativeColumn(1.7f);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1.8f);
                        });

                        static IContainer Celda(IContainer x) => x.BorderBottom(1).BorderColor(Borde).PaddingVertical(7).PaddingHorizontal(4);
                        static IContainer Encabezado(IContainer x) => x.Background(Oscuro).PaddingVertical(6).PaddingHorizontal(4);

                        table.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("#").FontColor(Colors.White).Bold().FontSize(8.5f);
                            h.Cell().Element(Encabezado).Text("Concepto").FontColor(Colors.White).Bold().FontSize(8.5f);
                            h.Cell().Element(Encabezado).AlignRight().Text("Cantidad").FontColor(Colors.White).Bold().FontSize(8.5f);
                            h.Cell().Element(Encabezado).AlignRight().Text("Precio unitario").FontColor(Colors.White).Bold().FontSize(8.5f);
                            h.Cell().Element(Encabezado).AlignRight().Text("Desc.").FontColor(Colors.White).Bold().FontSize(8.5f);
                            h.Cell().Element(Encabezado).AlignRight().Text("Importe").FontColor(Colors.White).Bold().FontSize(8.5f);
                        });

                        var n = 1;
                        foreach (var l in c.Lineas.OrderBy(l => l.Orden))
                        {
                            table.Cell().Element(Celda).Text((n++).ToString()).FontColor(Tenue);
                            table.Cell().Element(Celda).Column(cc =>
                            {
                                cc.Item().Text(l.Concepto).SemiBold();
                                if (!string.IsNullOrWhiteSpace(l.Descripcion)) cc.Item().PaddingTop(2).Text(l.Descripcion).FontSize(8.5f).FontColor(Tenue);
                            });
                            table.Cell().Element(Celda).AlignRight().Text($"{l.Cantidad.ToString("#,##0.##", Mx)} {l.Unidad}".Trim());
                            table.Cell().Element(Celda).AlignRight().Text(l.PrecioUnitario.ToString("#,##0.00", Mx));
                            table.Cell().Element(Celda).AlignRight().Text(l.DescuentoPorcentaje > 0 ? $"{l.DescuentoPorcentaje.ToString("0.##", Mx)}%" : "—").FontColor(Tenue);
                            table.Cell().Element(Celda).AlignRight().Text(l.Importe.ToString("#,##0.00", Mx)).SemiBold();
                        }
                    });

                    // Totales
                    col.Item().AlignRight().Width(240).Column(t =>
                    {
                        void Fila(string etiqueta, string valor, bool fuerte = false)
                        {
                            t.Item().PaddingVertical(3).Row(r =>
                            {
                                var e = r.RelativeItem().Text(etiqueta);
                                var v = r.RelativeItem().AlignRight().Text(valor);
                                if (fuerte) { e.Bold().FontSize(11); v.Bold().FontSize(11).FontColor(Verde); }
                                else { e.FontColor(Tenue); }
                            });
                        }
                        if (c.Descuento > 0) Fila("Descuento aplicado", "−" + Dinero(c.Descuento));
                        Fila("Subtotal", Dinero(c.Subtotal));
                        Fila($"IVA {(c.TasaIva * 100).ToString("0.##", Mx)}%", Dinero(c.Iva));
                        t.Item().PaddingTop(2).LineHorizontal(1).LineColor(Oscuro);
                        Fila("Total", Dinero(c.Total), fuerte: true);
                    });

                    // Condiciones
                    col.Item().Border(1).BorderColor(Borde).Padding(12).Column(b =>
                    {
                        b.Item().Text("CONDICIONES").FontSize(7.5f).Bold().FontColor(Tenue).LetterSpacing(0.08f);
                        b.Item().PaddingTop(4).Text(TextosCotizacion.CondicionPago(c)).SemiBold();
                        if (!string.IsNullOrWhiteSpace(c.Condiciones))
                        {
                            foreach (var linea in c.Condiciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                b.Item().PaddingTop(3).Text("• " + linea).FontColor(Tenue);
                            }
                        }
                        b.Item().PaddingTop(3).Text($"• Cotización vigente por {c.VigenciaDias} días naturales a partir de su fecha de emisión.").FontColor(Tenue);
                    });
                });

                // ---------- Pie ----------
                page.Footer().PaddingTop(10).BorderTop(1).BorderColor(Borde).Row(row =>
                {
                    row.RelativeItem().Column(pieCol =>
                    {
                        pieCol.Item().Text($"{empresa?.NombreComercial ?? "Global App Suite"} · {c.FolioCompleto}").FontSize(8).FontColor(Tenue);
                        if (pie.Count > 0) pieCol.Item().Text(string.Join(" · ", pie)).FontSize(7.5f).FontColor(Tenue);
                    });
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Tenue));
                        t.Span("Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }
}
