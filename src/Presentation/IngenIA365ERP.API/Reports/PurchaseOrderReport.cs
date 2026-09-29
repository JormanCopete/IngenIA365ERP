using System.Globalization;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// La orden de compra en PDF (feature 012, I5, T790; FR-048; contracts/api.md §14.9, §17.1), en el molde de
/// <see cref="VoucherPrintReport"/>: encabezado de la cooperativa, el proveedor de su copia fiscal, la bodega de entrega, la entrega
/// esperada, las condiciones, las líneas con precio pactado y descuento, los impuestos estimados a la fecha de la orden y los totales.
/// Un borrador o una orden en aprobación llevan la marca «BORRADOR» o «EN APROBACIÓN» y no tienen número; una anulada, «ANULADA». La sirve
/// <c>GET /purchases/orders/{id}/pdf</c> y la adjunta el envío al proveedor (<see cref="SendPurchaseOrderCommand"/>). (nuevo)
/// </summary>
public static class PurchaseOrderReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(OrdenDeCompraImprimible o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var marca = o.Status switch
        {
            DocumentStatus.Draft => "BORRADOR — sin número",
            DocumentStatus.PendingApproval => "EN APROBACIÓN — sin número",
            DocumentStatus.Voided => $"ANULADA — {o.DisplayNumber}",
            DocumentStatus.Discarded => "DESCARTADA",
            _ => $"Número {o.DisplayNumber}",
        };

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
                            c.Item().Text(o.Cooperativa.Name).Bold().FontSize(13);
                            if (!string.IsNullOrWhiteSpace(o.Cooperativa.TaxId)) c.Item().Text($"NIT {o.Cooperativa.TaxId}");
                            if (!string.IsNullOrWhiteSpace(o.Cooperativa.Address)) c.Item().Text(o.Cooperativa.Address);
                            var contacto = string.Join(" · ", new[] { o.Cooperativa.City, o.Cooperativa.Phone }.Where(x => !string.IsNullOrWhiteSpace(x)));
                            if (contacto.Length > 0) c.Item().Text(contacto).FontSize(8);
                        });
                        row.ConstantItem(220).AlignRight().Column(c =>
                        {
                            c.Item().Text("ORDEN DE COMPRA").Bold().FontSize(13);
                            c.Item().Text(o.DocumentTypeName).FontSize(8);
                            c.Item().Text(marca).Bold().FontSize(11);
                            c.Item().Text($"Fecha {o.OperationDate.ToString("dd/MM/yyyy", Co)}");
                            if (o.ExpectedDate is { } entrega) c.Item().Text($"Entrega esperada {entrega.ToString("dd/MM/yyyy", Co)}").SemiBold();
                        });
                    });
                    col.Item().PaddingTop(4).PaddingBottom(4).LineHorizontal(1);
                });

                page.Content().PaddingVertical(4).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Proveedor").Bold();
                            c.Item().Text(o.Proveedor.Name);
                            if (!string.IsNullOrWhiteSpace(o.Proveedor.TaxId))
                                c.Item().Text($"NIT {o.Proveedor.TaxId}{(string.IsNullOrWhiteSpace(o.Proveedor.CheckDigit) ? string.Empty : $"-{o.Proveedor.CheckDigit}")}");
                            if (!string.IsNullOrWhiteSpace(o.Proveedor.Address)) c.Item().Text(o.Proveedor.Address);
                            var contacto = string.Join(" · ", new[] { o.Proveedor.Email, o.Proveedor.Phone }.Where(x => !string.IsNullOrWhiteSpace(x)));
                            if (contacto.Length > 0) c.Item().Text(contacto).FontSize(8);
                        });
                        row.ConstantItem(20);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Entregar en").Bold();
                            if (o.Bodega is { } b)
                            {
                                c.Item().Text($"{b.Name} ({b.Code})");
                                if (!string.IsNullOrWhiteSpace(b.Address)) c.Item().Text(b.Address);
                            }
                            if (!string.IsNullOrWhiteSpace(o.PaymentTerms))
                            {
                                c.Item().PaddingTop(4).Text("Condiciones").Bold();
                                c.Item().Text(o.PaymentTerms);
                            }
                        });
                    });

                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(20);   // #
                            cols.ConstantColumn(60);   // código
                            cols.RelativeColumn(3);    // producto
                            cols.ConstantColumn(40);   // unidad
                            cols.ConstantColumn(55);   // cantidad
                            cols.ConstantColumn(70);   // precio
                            cols.ConstantColumn(60);   // descuento
                            cols.ConstantColumn(75);   // neto
                        });
                        table.Header(h =>
                        {
                            foreach (var (titulo, derecha) in new[] { ("#", false), ("Código", false), ("Producto", false), ("Unidad", false),
                                         ("Cantidad", true), ("Precio", true), ("Descuento", true), ("Valor", true) })
                            {
                                var celda = h.Cell().BorderBottom(1).Padding(2);
                                if (derecha) celda.AlignRight().Text(titulo).Bold(); else celda.Text(titulo).Bold();
                            }
                        });
                        foreach (var l in o.Lineas)
                        {
                            table.Cell().Padding(2).Text(l.LineNumber.ToString(Co));
                            table.Cell().Padding(2).Text(l.ProductCode);
                            table.Cell().Padding(2).Text(l.ProductName);
                            table.Cell().Padding(2).Text(l.UnitCode);
                            table.Cell().Padding(2).AlignRight().Text(l.Quantity.ToString("0.####", Co));
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.UnitPrice));
                            table.Cell().Padding(2).AlignRight().Text(l.DiscountAmount > 0m ? Pesos(l.DiscountAmount) : string.Empty);
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.NetAmount));
                        }
                    });

                    col.Item().PaddingTop(6).AlignRight().Width(260).Column(c =>
                    {
                        Total(c, "Subtotal", o.Subtotal, false);
                        if (o.DiscountTotal > 0m) Total(c, "Descuentos", -o.DiscountTotal, false);
                        foreach (var i in o.Impuestos)
                        {
                            var tarifa = i.Rate is { } r ? $" {(r * 100m).ToString("0.##", Co)} %" : string.Empty;
                            Total(c, $"{i.Kind}{tarifa} sobre {Pesos(i.Base)}", i.Amount, false);
                        }
                        Total(c, "Total", o.Total, true);
                        c.Item().PaddingTop(2).Text("Impuestos estimados a la fecha de la orden; las retenciones las liquida la factura.").FontSize(7).Italic();
                    });

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        Firma(row, "Elaboró", o.ElaboradaPor ?? string.Empty);
                        row.ConstantItem(30);
                        Firma(row, "Aprobó", string.Empty);
                        row.ConstantItem(30);
                        Firma(row, "Proveedor (recibido)", string.Empty);
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"Orden de compra {o.DisplayNumber ?? "(borrador)"} · impresa el {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · página ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" de ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static void Total(ColumnDescriptor c, string rotulo, decimal valor, bool resaltado) =>
        c.Item().Row(r =>
        {
            var texto = r.RelativeItem().Text(rotulo);
            var monto = r.ConstantItem(90).AlignRight().Text(Pesos(valor));
            if (resaltado)
            {
                texto.Bold();
                monto.Bold();
            }
        });

    private static void Firma(RowDescriptor row, string rotulo, string quien) =>
        row.RelativeItem().Column(c =>
        {
            c.Item().LineHorizontal(0.5f);
            c.Item().AlignCenter().Text(rotulo).FontSize(8).Bold();
            c.Item().AlignCenter().Text(quien).FontSize(8);
        });

    private static string Pesos(decimal v) => v.ToString("N2", Co);
}

/// <summary><see cref="IOrdenDeCompraEnPdf"/> con QuestPDF. Registrado en la API, la única capa que conoce la librería. (nuevo)</summary>
public sealed class PurchaseOrderPdfRenderer : IOrdenDeCompraEnPdf
{
    public byte[] Generar(OrdenDeCompraImprimible orden) => PurchaseOrderReport.Generate(orden);
}
