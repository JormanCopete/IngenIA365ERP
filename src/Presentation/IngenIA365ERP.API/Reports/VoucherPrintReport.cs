using System.Globalization;
using IngenIA365ERP.Application.Accounting.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Impresión de un comprobante contable (feature 009, US3; <c>GET /api/accounting/documents/{id}/print</c>).
/// Reescrito sobre <see cref="ComprobanteDto"/>: origen (módulo y documento), quién registró y
/// quién contabilizó, reversión en ambos sentidos y tercero, documento cruce, centro y sucursal
/// por línea. Un borrador se imprime con marca de agua «BORRADOR» y sin número.
/// </summary>
public static class VoucherPrintReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(ComprobanteDto d, EmpresaParaImpresion empresa)
    {
        var esBorrador = string.Equals(d.Status, "Draft", StringComparison.OrdinalIgnoreCase);
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8.5f));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(empresa.Name).Bold().FontSize(13);
                            if (!string.IsNullOrWhiteSpace(empresa.TaxId)) c.Item().Text($"NIT {empresa.TaxId}").FontSize(8);
                        });
                        row.ConstantItem(260).AlignRight().Column(c =>
                        {
                            c.Item().Text($"{d.VoucherTypeName} ({d.VoucherTypeCode})").SemiBold().FontSize(11);
                            c.Item().Text(esBorrador ? "BORRADOR — sin número" : $"Número {d.Referencia}").Bold().FontSize(11);
                            c.Item().Text($"Fecha {d.Date:dd/MM/yyyy}   Estado {Estado(d.Status)}");
                        });
                    });
                    col.Item().PaddingTop(4).Text($"Concepto: {d.Description}");
                    col.Item().Text($"Origen: {d.Origin.ModuleName}{(d.Origin.SourceType is null || d.Origin.Module == "CNT" ? string.Empty : $" · {d.Origin.SourceType} {d.Origin.SourcePublicId}")}").FontSize(8);
                    if (d.Reversal.ReversesPublicId is not null)
                        col.Item().Text($"Reversa el comprobante {d.Reversal.ReversesNumber}. Motivo: {d.Reversal.Reason}").FontSize(8).Italic();
                    if (d.Reversal.ReversedByPublicId is not null)
                        col.Item().Text($"REVERSADO por el comprobante {d.Reversal.ReversedByNumber}. Motivo: {d.Reversal.Reason}").FontSize(8).Bold();
                    col.Item().PaddingTop(3).PaddingBottom(4).LineHorizontal(1);
                });

                page.Content().PaddingVertical(4).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(18);   // #
                            cols.ConstantColumn(62);   // código
                            cols.RelativeColumn(2.2f); // cuenta
                            cols.RelativeColumn(2);    // tercero
                            cols.ConstantColumn(70);   // doc. cruce
                            cols.RelativeColumn(1.2f); // centro
                            cols.RelativeColumn(1);    // sucursal
                            cols.RelativeColumn(2);    // detalle
                            cols.ConstantColumn(78);   // débito
                            cols.ConstantColumn(78);   // crédito
                        });

                        table.Header(h =>
                        {
                            foreach (var (titulo, derecha) in new[] { ("#", false), ("Código", false), ("Cuenta", false), ("Tercero", false), ("Doc. cruce", false), ("Centro", false), ("Sucursal", false), ("Detalle", false), ("Débito", true), ("Crédito", true) })
                            {
                                var celda = h.Cell().BorderBottom(1).Padding(2);
                                if (derecha) celda.AlignRight().Text(titulo).Bold(); else celda.Text(titulo).Bold();
                            }
                        });

                        foreach (var l in d.Lines)
                        {
                            table.Cell().Padding(2).Text(l.LineNumber.ToString(Co)).FontSize(7.5f);
                            table.Cell().Padding(2).Text(l.AccountCode).FontSize(7.5f);
                            table.Cell().Padding(2).Text(l.AccountName).FontSize(7.5f);
                            table.Cell().Padding(2).Text(l.PersonName is null ? string.Empty : $"{l.PersonTaxId} {l.PersonName}").FontSize(7.5f);
                            table.Cell().Padding(2).Text(l.CrossDocumentType is null ? string.Empty : $"{l.CrossDocumentType} {l.CrossDocumentNumber}").FontSize(7.5f);
                            table.Cell().Padding(2).Text(l.CostCenterName ?? string.Empty).FontSize(7.5f);
                            table.Cell().Padding(2).Text(l.BranchName).FontSize(7.5f);
                            table.Cell().Padding(2).Text(l.Detail ?? string.Empty).FontSize(7.5f);
                            table.Cell().Padding(2).AlignRight().Text(l.Debit > 0 ? Pesos(l.Debit) : string.Empty).FontSize(7.5f);
                            table.Cell().Padding(2).AlignRight().Text(l.Credit > 0 ? Pesos(l.Credit) : string.Empty).FontSize(7.5f);
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text(d.TotalDebit == d.TotalCredit ? "TOTALES" : $"TOTALES — descuadre {Pesos(d.TotalDebit - d.TotalCredit)}").Bold();
                        row.ConstantItem(78).AlignRight().Text(Pesos(d.TotalDebit)).Bold();
                        row.ConstantItem(78).AlignRight().Text(Pesos(d.TotalCredit)).Bold();
                    });

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        Firma(row, "Registró", d.RegisteredBy, d.RegisteredAt);
                        row.ConstantItem(30);
                        Firma(row, "Contabilizó", d.PostedBy ?? "—", d.PostedAt);
                        row.ConstantItem(30);
                        Firma(row, "Revisó", string.Empty, null);
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"{d.VoucherTypeCode} {(esBorrador ? "borrador" : d.Referencia)} · impreso el {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · página ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" de ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static void Firma(RowDescriptor row, string rotulo, string quien, DateTime? cuando) =>
        row.RelativeItem().Column(c =>
        {
            c.Item().LineHorizontal(0.5f);
            c.Item().AlignCenter().Text(rotulo).FontSize(8).Bold();
            c.Item().AlignCenter().Text(quien).FontSize(8);
            if (cuando is { } f) c.Item().AlignCenter().Text(f.ToString("yyyy-MM-dd HH:mm", Co)).FontSize(7);
        });

    private static string Estado(string status) => status switch
    {
        "Draft" => "Borrador",
        "Posted" => "Contabilizado",
        "Reversed" => "Reversado",
        _ => status,
    };

    private static string Pesos(decimal v) => v.ToString("N2", Co);
}

/// <summary><see cref="IVoucherPdfRenderer"/> con QuestPDF. Registrado en la API, la única capa que conoce la librería.</summary>
public sealed class VoucherPdfRenderer : IVoucherPdfRenderer
{
    public byte[] Render(ComprobanteDto comprobante, EmpresaParaImpresion empresa) => VoucherPrintReport.Generate(comprobante, empresa);
}
