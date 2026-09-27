using System.Globalization;
using IngenIA365ERP.Application.Inventory.Sales;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// El comprobante no electrónico en carta (feature 012, I3, T626; contracts/api.md §20.3, §27): el mismo <see cref="TicketDto"/> de la
/// tirilla —el comprador de la <b>copia fiscal vigente</b>, los impuestos guardados al confirmar y los pagos con sólo los últimos
/// cuatro de una tarjeta—, pintado en carta con QuestPDF. Lleva las marcas del pie («COPIA», «SIN VALIDEZ FISCAL») como sello visible.
/// La representación gráfica de los documentos electrónicos es de I4 (<c>RepresentacionGraficaReport</c>). (nuevo)
/// </summary>
public static class SalesDocumentReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(TicketDto t)
    {
        ArgumentNullException.ThrowIfNull(t);
        var numero = t.Document.Number is { } n ? $"{t.Document.Prefix}{n.ToString(CultureInfo.InvariantCulture)}" : "sin número";
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t.Header.CompanyName).Bold().FontSize(13);
                            if (!string.IsNullOrWhiteSpace(t.Header.Nit)) c.Item().Text($"NIT {t.Header.Nit}");
                            c.Item().Text(t.Header.BranchName);
                            if (!string.IsNullOrWhiteSpace(t.Header.Address)) c.Item().Text(t.Header.Address);
                            if (!string.IsNullOrWhiteSpace(t.Header.RegimeText)) c.Item().Text(t.Header.RegimeText).FontSize(8);
                        });
                        row.ConstantItem(220).AlignRight().Column(c =>
                        {
                            c.Item().Text(t.Document.ClassLabel).SemiBold().FontSize(12);
                            c.Item().Text($"Número {numero}").Bold().FontSize(12);
                            if (t.Document.IssuedAt is { } fecha) c.Item().Text($"Fecha {fecha.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Co)}");
                            if (!string.IsNullOrWhiteSpace(t.Document.CashRegisterCode)) c.Item().Text($"Caja {t.Document.CashRegisterCode}");
                            if (!string.IsNullOrWhiteSpace(t.Document.CashierName)) c.Item().Text($"Cajero {t.Document.CashierName}");
                            if (!string.IsNullOrWhiteSpace(t.Document.SalespersonName)) c.Item().Text($"Vendedor {t.Document.SalespersonName}");
                        });
                    });
                    if (!string.IsNullOrWhiteSpace(t.Header.ResolutionText)) col.Item().PaddingTop(2).Text(t.Header.ResolutionText).FontSize(8);
                    col.Item().PaddingTop(6).Text($"Cliente: {t.Party.Name} · {t.Party.IdType} {t.Party.IdNumber}");
                    col.Item().PaddingTop(4).PaddingBottom(4).LineHorizontal(1);
                });

                page.Content().Column(col =>
                {
                    foreach (var marca in t.Footer) col.Item().AlignCenter().Text(marca).Bold().FontSize(14);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(60);
                            c.RelativeColumn(3);
                            c.ConstantColumn(55);
                            c.ConstantColumn(35);
                            c.ConstantColumn(70);
                            c.ConstantColumn(60);
                            c.ConstantColumn(75);
                            c.ConstantColumn(40);
                        });
                        table.Header(h =>
                        {
                            foreach (var (titulo, derecha) in new[] { ("Código", false), ("Descripción", false), ("Cantidad", true), ("Unidad", false), ("Precio", true), ("Descuento", true), ("Total", true), ("Imp.", false) })
                            {
                                var celda = h.Cell().BorderBottom(1).Padding(2);
                                if (derecha) celda.AlignRight().Text(titulo).Bold(); else celda.Text(titulo).Bold();
                            }
                        });
                        foreach (var l in t.Lines)
                        {
                            table.Cell().Padding(2).Text(l.Code);
                            table.Cell().Padding(2).Text(l.Description);
                            table.Cell().Padding(2).AlignRight().Text(l.Quantity.ToString("0.####", Co));
                            table.Cell().Padding(2).Text(l.UnitCode);
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.UnitPrice));
                            table.Cell().Padding(2).AlignRight().Text(l.Discount == 0m ? string.Empty : Pesos(l.Discount));
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.Total));
                            table.Cell().Padding(2).Text(l.TaxMark);
                        }
                    });

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            if (t.Taxes.Count > 0)
                            {
                                c.Item().Text("Impuestos").Bold();
                                foreach (var i in t.Taxes) c.Item().Text($"{i.Label}: base {Pesos(i.Base)} · valor {Pesos(i.Amount)}");
                            }
                            if (t.Withholdings.Count > 0)
                            {
                                c.Item().PaddingTop(4).Text("Retenciones").Bold();
                                foreach (var r in t.Withholdings) c.Item().Text($"{r.Label}: {Pesos(r.Amount)}");
                            }
                            if (t.Payments.Count > 0)
                            {
                                c.Item().PaddingTop(4).Text("Pagos").Bold();
                                foreach (var p in t.Payments)
                                {
                                    var detalle = p.Last4 is { Length: > 0 } ultimos ? $" · **** {ultimos}" : p.Reference is { Length: > 0 } referencia ? $" · {referencia}" : string.Empty;
                                    c.Item().Text($"{p.MeansName}{detalle}: {Pesos(p.Amount)}");
                                }
                                if (t.Change > 0m) c.Item().Text($"Cambio: {Pesos(t.Change)}");
                            }
                        });
                        row.ConstantItem(200).Column(c =>
                        {
                            Total(c, "Subtotal", t.Totals.Subtotal);
                            Total(c, "Descuentos", t.Totals.DiscountTotal);
                            Total(c, "Impuestos", t.Totals.TaxTotal);
                            Total(c, "Total", t.Totals.Total, negrita: true);
                            if (t.Totals.AmountDue != t.Totals.Total) Total(c, "A pagar", t.Totals.AmountDue, negrita: true);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"{t.Document.ClassLabel} {numero} · página ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" de ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        });
        return documento.GeneratePdf();
    }

    private static void Total(ColumnDescriptor c, string rotulo, decimal valor, bool negrita = false) =>
        c.Item().Row(r =>
        {
            var izquierda = r.RelativeItem().Text(rotulo);
            var derecha = r.ConstantItem(100).AlignRight().Text(Pesos(valor));
            if (negrita)
            {
                izquierda.Bold();
                derecha.Bold();
            }
        });

    private static string Pesos(decimal v) => v.ToString("N2", Co);
}

/// <summary><see cref="IRepresentacionDeVentaEnPdf"/> con QuestPDF: la carta y el correo de la entrega y la reimpresión (T607). (nuevo)</summary>
public sealed class SalesDocumentPdfRenderer : IRepresentacionDeVentaEnPdf
{
    public Task<byte[]> GenerarAsync(TicketDto modelo, CancellationToken ct) => Task.FromResult(SalesDocumentReport.Generate(modelo));
}
