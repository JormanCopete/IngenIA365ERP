using System.Globalization;
using IngenIA365ERP.Application.Lending.SignatureDocuments;
using IngenIA365ERP.Domain.Lending.SignatureDocuments;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Los documentos para firma de una solicitud en PDF (2026-11-18, pedido del dueño): el pagaré, la carta de instrucciones, la libranza y los demás que
/// la cooperativa configure, cada uno desde una hoja nueva, en carta, con el membrete discreto de la cooperativa arriba y la referencia y el número
/// de página abajo. El cuerpo sale de <see cref="PlantillaDeFirma"/>: títulos, párrafos justificados, viñetas, centrados, saltos de hoja y el bloque
/// de firmas (nombre, documento, calidad y, si la plantilla lo pide, el recuadro de la huella). No calcula ni lee la base. (nuevo)
/// </summary>
public static class DocumentosParaFirmaReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(PaqueteParaFirma paquete)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        return Document.Create(container =>
            {
                foreach (var documento in paquete.Documentos) Pagina(container, paquete, documento);
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Documentos para firma · {paquete.Referencia}",
                Author = paquete.Cooperativa,
                Creator = "IngenIA365ERP",
                Producer = "IngenIA365ERP",
                CreationDate = paquete.GeneradoEn,
                ModifiedDate = paquete.GeneradoEn,
            })
            .GeneratePdf();
    }

    private static void Pagina(IDocumentContainer container, PaqueteParaFirma paquete, DocumentoParaImprimir documento)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.MarginHorizontal(60);
            page.MarginVertical(45);
            page.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.25f));

            if (paquete.MarcaDeAgua is { } marca)
                page.Background().AlignCenter().AlignMiddle().Rotate(-30).Text(marca).FontSize(40).Bold().FontColor(Colors.Grey.Lighten2);

            page.Header().PaddingBottom(10).Row(row =>
            {
                if (paquete.Logotipo is { Length: > 0 } logo) row.ConstantItem(70).Height(40).Image(logo).FitArea();
                row.RelativeItem().PaddingLeft(paquete.Logotipo is { Length: > 0 } ? 8 : 0).AlignMiddle().Column(c =>
                {
                    c.Item().Text(paquete.Cooperativa).SemiBold().FontSize(10);
                    if (paquete.Nit is { } nit) c.Item().Text($"NIT {nit}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                });
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"{documento.Nombre} · {paquete.Referencia}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                row.ConstantItem(90).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Colors.Grey.Darken1));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });

            page.Content().Column(col =>
            {
                foreach (var bloque in documento.Bloques) Bloque(col, bloque, documento);
            });
        });
    }

    private static void Bloque(ColumnDescriptor col, BloqueDeTexto bloque, DocumentoParaImprimir documento)
    {
        switch (bloque.Tipo)
        {
            case TipoDeBloque.Titulo:
                col.Item().PaddingBottom(6).AlignCenter().Text(t => Tramos(t, bloque, 13, siempreNegrita: true));
                break;
            case TipoDeBloque.Subtitulo:
                col.Item().PaddingTop(6).PaddingBottom(3).Text(t => Tramos(t, bloque, 11, siempreNegrita: true));
                break;
            case TipoDeBloque.Centrado:
                col.Item().PaddingBottom(4).AlignCenter().Text(t => Tramos(t, bloque));
                break;
            case TipoDeBloque.Vineta:
                col.Item().PaddingBottom(4).PaddingLeft(12).Row(row =>
                {
                    row.ConstantItem(12).Text("•");
                    row.RelativeItem().Text(t =>
                    {
                        t.Justify();
                        Tramos(t, bloque);
                    });
                });
                break;
            case TipoDeBloque.Espacio:
                col.Item().Height(6);
                break;
            case TipoDeBloque.SaltoDePagina:
                col.Item().PageBreak();
                break;
            case TipoDeBloque.Firmas:
                Firmas(col, documento);
                break;
            default:
                col.Item().PaddingBottom(4).Text(t =>
                {
                    t.Justify();
                    Tramos(t, bloque);
                });
                break;
        }
    }

    private static void Tramos(TextDescriptor t, BloqueDeTexto bloque, float? tamano = null, bool siempreNegrita = false)
    {
        foreach (var tramo in bloque.Tramos)
        {
            var span = t.Span(tramo.Texto);
            if (tramo.Negrita || siempreNegrita) span.Bold();
            if (tamano is { } f) span.FontSize(f);
        }
    }

    /// <summary>Las firmas, de a dos por fila, cada una entera en su hoja: espacio, raya, nombre, documento, calidad y, si se pide, la huella.</summary>
    private static void Firmas(ColumnDescriptor col, DocumentoParaImprimir documento)
    {
        var firmantes = documento.Firmantes;
        for (var i = 0; i < firmantes.Count; i += 2)
        {
            var par = firmantes.Skip(i).Take(2).ToList();
            col.Item().PaddingTop(22).ShowEntire().Row(row =>
            {
                foreach (var f in par)
                {
                    row.RelativeItem().PaddingRight(14).Row(firma =>
                    {
                        firma.RelativeItem().Column(c =>
                        {
                            c.Item().Height(38);
                            c.Item().LineHorizontal(0.8f);
                            c.Item().PaddingTop(2).Text(f.Nombre).Bold().FontSize(9.5f);
                            c.Item().Text($"{f.TipoDeDocumento} {f.Documento}").FontSize(9);
                            c.Item().Text(f.Calidad).FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                        });
                        if (documento.Huella)
                            firma.ConstantItem(62).PaddingLeft(8).Column(h =>
                            {
                                h.Item().Height(64).Border(0.8f);
                                h.Item().AlignCenter().Text("Huella").FontSize(7);
                            });
                    });
                }

                if (par.Count == 1) row.RelativeItem();
            });
        }
    }

    /// <summary>Los textos de los bloques en orden (el PDF los lleva como glifos, que una prueba no puede leer).</summary>
    public static IReadOnlyList<string> TextosImpresos(PaqueteParaFirma paquete) =>
        paquete.Documentos.SelectMany(d => d.Bloques.Select(b => b.TextoPlano).Concat(
            d.Bloques.Any(b => b.Tipo == TipoDeBloque.Firmas) ? d.Firmantes.Select(f => $"{f.Nombre} {f.TipoDeDocumento} {f.Documento}") : [])).ToList();
}

/// <summary><see cref="IDocumentosParaFirmaRenderer"/> con QuestPDF. (nuevo)</summary>
public sealed class DocumentosParaFirmaPdfRenderer : IDocumentosParaFirmaRenderer
{
    public byte[] Render(PaqueteParaFirma paquete) => DocumentosParaFirmaReport.Generate(paquete);
}
