using System.Globalization;
using IngenIA365ERP.Application.Accounting.Taxes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// El certificado de retención en PDF (feature 009 E4, US10, FR-067): fuente, IVA o ICA, con número, año o meses, fecha de expedición,
/// líneas por concepto con base, tarifa y valor retenido. Reimprimir dibuja lo guardado (el número no cambia); uno anulado sale marcado.
/// Hasta E4 esta clase era el certificado del sistema anterior y no la llamaba nadie.
/// </summary>
public static class WithholdingCertificateReport
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(CertificadoParaImprimir data)
    {
        var periodo = data.PeriodFrom is { } desde && data.PeriodTo is { } hasta
            ? $"{Mes(desde)} a {Mes(hasta)} de {data.Year}"
            : $"Año gravable {data.Year}";

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
                    col.Item().AlignCenter().Text($"NIT {data.CompanyTaxId}").FontSize(10);
                    if (!string.IsNullOrWhiteSpace(data.CompanyAddress)) col.Item().AlignCenter().Text(data.CompanyAddress).FontSize(8);
                    col.Item().PaddingTop(10).AlignCenter().Text(data.Title).Bold().FontSize(12);
                    col.Item().AlignCenter().Text($"{periodo} · Certificado n.º {data.Number}").FontSize(10);
                    col.Item().AlignCenter().Text($"Expedido el {data.IssuedAt.ToLocalTime().ToString("dd 'de' MMMM 'de' yyyy", Cultura)}").FontSize(8);
                    if (data.Voided) col.Item().AlignCenter().Text("ANULADO — REEMPLAZADO POR UNO POSTERIOR").Bold().FontSize(11);
                    col.Item().PaddingTop(6).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text("Retenido").Bold();
                    col.Item().Row(row => { row.ConstantItem(100).Text("Nombre o razón social").SemiBold(); row.RelativeItem().Text(data.PersonName); });
                    col.Item().Row(row => { row.ConstantItem(100).Text("Identificación").SemiBold(); row.RelativeItem().Text(data.PersonTaxId); });
                    if (!string.IsNullOrWhiteSpace(data.PersonAddress))
                        col.Item().Row(row => { row.ConstantItem(100).Text("Dirección").SemiBold(); row.RelativeItem().Text(data.PersonAddress); });

                    col.Item().PaddingVertical(10).LineHorizontal(0.5f);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(70);
                            cols.RelativeColumn();
                            cols.ConstantColumn(95);
                            cols.ConstantColumn(55);
                            cols.ConstantColumn(95);
                        });
                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(3).Text("Concepto").Bold();
                            header.Cell().BorderBottom(1).Padding(3).Text("Descripción").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Base").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Tarifa").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Retenido").Bold();
                        });
                        foreach (var line in data.Lines)
                        {
                            table.Cell().Padding(3).Text(line.ConceptCode);
                            table.Cell().Padding(3).Text(line.ConceptName);
                            table.Cell().Padding(3).AlignRight().Text(line.Base.ToString("N2", Cultura));
                            table.Cell().Padding(3).AlignRight().Text((line.Rate * 100m).ToString("0.###", Cultura) + " %");
                            table.Cell().Padding(3).AlignRight().Text(line.Amount.ToString("N2", Cultura));
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("Totales").Bold();
                        row.ConstantItem(95).AlignRight().Text(data.TotalBase.ToString("N2", Cultura)).Bold();
                        row.ConstantItem(55);
                        row.ConstantItem(95).AlignRight().Text(data.TotalAmount.ToString("N2", Cultura)).Bold();
                    });

                    col.Item().PaddingTop(15).Text(
                        $"{data.CompanyName} certifica que en el período indicado practicó a {data.PersonName} la {data.KindName.ToLowerInvariant()} detallada, " +
                        "que fue declarada y consignada en los plazos correspondientes.").FontSize(8).Italic();
                    col.Item().PaddingTop(4).Text("Documento generado por sistema; no requiere firma autógrafa.").FontSize(7);
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string Mes(byte mes) => Cultura.DateTimeFormat.GetMonthName(mes);
}

/// <summary><see cref="IWithholdingCertificatePdfRenderer"/> con QuestPDF. Registrado en la API, la única capa que conoce la librería.</summary>
public sealed class WithholdingCertificatePdfRenderer : IWithholdingCertificatePdfRenderer
{
    public byte[] Render(CertificadoParaImprimir certificado) => WithholdingCertificateReport.Generate(certificado);
}
