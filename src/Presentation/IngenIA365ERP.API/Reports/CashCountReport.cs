using System.Globalization;
using IngenIA365ERP.Application.Inventory.Cash;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// El informe de arqueo de una sesión de caja (feature 012, I3, T626; contracts/api.md §21.2, §27): por medio, esperado, contado,
/// diferencia, tolerancia, tratamiento y motivo; debajo de cada medio, las denominaciones contadas, los lotes de cada datáfono y las
/// referencias cotejadas; los totales, el documento de diferencia y las firmas del cajero y del supervisor. (nuevo)
/// </summary>
public static class CashCountReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(CashCountReportModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(8.5f));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(m.Company.Name).Bold().FontSize(12);
                            if (!string.IsNullOrWhiteSpace(m.Company.Nit)) c.Item().Text($"NIT {m.Company.Nit}");
                        });
                        row.ConstantItem(240).AlignRight().Column(c =>
                        {
                            c.Item().Text("Arqueo de caja").SemiBold().FontSize(12);
                            c.Item().Text($"Punto {m.PointOfSale} · caja {m.CashRegister}");
                            c.Item().Text($"Fecha operativa {m.OperatingDate:yyyy-MM-dd}");
                        });
                    });
                    col.Item().PaddingTop(3).Text($"Cajero: {m.Cashier} · apertura {Hora(m.OpenedAt)} · arqueo {Hora(m.CountedAt)}"
                        + (m.ClosedAt is { } cierre ? $" · cierre {Hora(cierre)}" : string.Empty) + (m.Blind ? " · arqueo ciego" : string.Empty));
                    col.Item().PaddingTop(3).PaddingBottom(4).LineHorizontal(1);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.4f);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                            c.ConstantColumn(60);
                            c.RelativeColumn(1.6f);
                        });
                        table.Header(h =>
                        {
                            foreach (var (titulo, derecha) in new[] { ("Medio", false), ("Esperado", true), ("Contado", true), ("Diferencia", true), ("Tolerancia", true), ("Tratamiento / motivo", false) })
                            {
                                var celda = h.Cell().BorderBottom(1).Padding(2);
                                if (derecha) celda.AlignRight().Text(titulo).Bold(); else celda.Text(titulo).Bold();
                            }
                        });
                        foreach (var l in m.Lines)
                        {
                            table.Cell().Padding(2).Text($"{l.PaymentMeansCode} · {l.PaymentMeansName}").SemiBold();
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.Expected));
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.Counted));
                            var diferencia = table.Cell().Padding(2).AlignRight().Text(Pesos(l.Difference));
                            if (!l.WithinTolerance) diferencia.Bold();
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.Tolerance));
                            table.Cell().Padding(2).Text(string.Join(" · ", new[] { l.Treatment, l.Reason }.Where(x => !string.IsNullOrWhiteSpace(x))));

                            foreach (var d in l.Denominations)
                                Detalle(table, $"   {Pesos(d.Value)} × {d.Quantity.ToString(Co)}", d.Amount);
                            foreach (var b in l.TerminalBatches)
                                Detalle(table, $"   Datáfono {b.CardTerminalCode} · lote {b.BatchNumber} · {b.VoucherCount.ToString(Co)} comprobantes (esperado {Pesos(b.ExpectedTotal)})", b.BatchTotal);
                            foreach (var r in l.References)
                                Detalle(table, $"   {(r.Verified ? "✓" : "✗")} {r.DocumentNumber} {r.Reference}{(string.IsNullOrWhiteSpace(r.Note) ? string.Empty : $" · {r.Note}")}", r.Amount);
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("TOTALES").Bold();
                        row.ConstantItem(72).AlignRight().Text(Pesos(m.TotalExpected)).Bold();
                        row.ConstantItem(72).AlignRight().Text(Pesos(m.TotalCounted)).Bold();
                        row.ConstantItem(72).AlignRight().Text(Pesos(m.TotalDifference)).Bold();
                        row.ConstantItem(60);
                        row.RelativeItem(1.6f);
                    });
                    if (m.DifferenceDocumentNumber is not null)
                        col.Item().PaddingTop(4).Text($"Documento de diferencia {m.DifferenceDocumentNumber} · {m.DifferenceDocumentStatus}");

                    col.Item().PaddingTop(40).Row(row =>
                    {
                        Firma(row, "Cajero", m.Cashier);
                        row.ConstantItem(40);
                        Firma(row, "Supervisor", m.ClosedBy ?? string.Empty);
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"Arqueo {m.CashRegister} {m.OperatingDate:yyyy-MM-dd} · página ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" de ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        });
        return documento.GeneratePdf();
    }

    private static void Detalle(TableDescriptor table, string texto, decimal valor)
    {
        table.Cell().PaddingLeft(6).Text(texto).FontSize(7.5f);
        table.Cell();
        table.Cell().AlignRight().Text(Pesos(valor)).FontSize(7.5f);
        table.Cell();
        table.Cell();
        table.Cell();
    }

    internal static void Firma(RowDescriptor row, string rotulo, string quien) =>
        row.RelativeItem().Column(c =>
        {
            c.Item().LineHorizontal(0.5f);
            c.Item().AlignCenter().Text(rotulo).Bold();
            c.Item().AlignCenter().Text(quien);
        });

    private static string Hora(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Co);

    internal static string Pesos(decimal v) => v.ToString("N2", Co);
}

/// <summary><see cref="IDocumentosDeCajaEnPdf"/> con QuestPDF: el arqueo y el comprobante de movimiento. (nuevo)</summary>
public sealed class DocumentosDeCajaPdfRenderer : IDocumentosDeCajaEnPdf
{
    public byte[] Arqueo(CashCountReportModel modelo) => CashCountReport.Generate(modelo);

    public byte[] ComprobanteDeMovimiento(CashMovementReceiptModel modelo) => CashMovementReceiptReport.Generate(modelo);
}
