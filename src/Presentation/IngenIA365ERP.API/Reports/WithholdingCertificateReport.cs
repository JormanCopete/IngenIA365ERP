using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record WithholdingLineDto(
    string ConceptCode,
    string ConceptName,
    decimal TaxBase,
    decimal Rate,
    decimal WithheldAmount);

public record WithholdingCertificateDto(
    string CompanyName,
    string CompanyNit,
    string CompanyAddress,
    string PersonName,
    string PersonNit,
    string PersonAddress,
    int Year,
    List<WithholdingLineDto> Lines,
    decimal TotalBase,
    decimal TotalWithheld,
    string SignatoryName,
    string SignatoryTitle);

public static class WithholdingCertificateReport
{
    public static byte[] Generate(WithholdingCertificateDto data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(data.CompanyName).Bold().FontSize(14);
                    col.Item().AlignCenter().Text($"NIT: {data.CompanyNit}").FontSize(10);
                    col.Item().AlignCenter().Text(data.CompanyAddress).FontSize(8);
                    col.Item().PaddingTop(10).AlignCenter().Text("CERTIFICADO DE RETENCION EN LA FUENTE").Bold().FontSize(12);
                    col.Item().AlignCenter().Text($"Ano Gravable: {data.Year}").FontSize(10);
                    col.Item().PaddingBottom(10).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Recipient info
                    col.Item().Text("DATOS DEL RETENIDO:").Bold();
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(100).Text("Nombre:").SemiBold();
                        row.RelativeItem().Text(data.PersonName);
                    });
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(100).Text("NIT/CC:").SemiBold();
                        row.RelativeItem().Text(data.PersonNit);
                    });
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(100).Text("Direccion:").SemiBold();
                        row.RelativeItem().Text(data.PersonAddress);
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(0.5f);

                    // Withholding table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(60);   // Code
                            cols.RelativeColumn();      // Concept
                            cols.ConstantColumn(100);  // Base
                            cols.ConstantColumn(60);   // Rate
                            cols.ConstantColumn(100);  // Withheld
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(3).Text("Codigo").Bold();
                            header.Cell().BorderBottom(1).Padding(3).Text("Concepto").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Base Gravable").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Tarifa %").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Valor Retenido").Bold();
                        });

                        foreach (var line in data.Lines)
                        {
                            table.Cell().Padding(3).Text(line.ConceptCode);
                            table.Cell().Padding(3).Text(line.ConceptName);
                            table.Cell().Padding(3).AlignRight().Text($"{line.TaxBase:N2}");
                            table.Cell().Padding(3).AlignRight().Text($"{line.Rate:N2}%");
                            table.Cell().Padding(3).AlignRight().Text($"{line.WithheldAmount:N2}");
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("TOTALES").Bold();
                        row.ConstantItem(100).AlignRight().Text($"{data.TotalBase:N2}").Bold();
                        row.ConstantItem(60);
                        row.ConstantItem(100).AlignRight().Text($"{data.TotalWithheld:N2}").Bold();
                    });

                    // Legal text
                    col.Item().PaddingTop(15).Text(
                        $"Certificamos que durante el ano gravable {data.Year}, se practicaron las retenciones " +
                        "en la fuente detalladas anteriormente, las cuales fueron declaradas y consignadas " +
                        "oportunamente ante la DIAN.").FontSize(8).Italic();

                    // Signature
                    col.Item().PaddingTop(40).Column(c =>
                    {
                        c.Item().ExtendHorizontal().LineHorizontal(0.5f);
                        c.Item().Text(data.SignatoryName).Bold();
                        c.Item().Text(data.SignatoryTitle).FontSize(8);
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Pagina ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
