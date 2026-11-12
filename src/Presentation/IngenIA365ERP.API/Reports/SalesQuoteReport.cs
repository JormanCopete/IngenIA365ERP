using System.Globalization;
using IngenIA365ERP.Application.Inventory.Sales.Quotes;
using IngenIA365ERP.Domain.Enums.Inventory;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// La cotización impresa para el cliente (2026-11-12, decisión del dueño), en el molde de <see cref="PurchaseOrderReport"/>: encabezado de la
/// cooperativa (nombre, NIT, dirección), número, fecha y «Vigente hasta», los datos del cliente y el vendedor, las líneas (código,
/// descripción, unidad, cantidad, precio, descuento, impuesto y total), el desglose de totales —subtotal, descuentos, impuestos y retenciones
/// por tarifa, total y valor a pagar—, las notas para el cliente y las «Condiciones de la cotización» de la cooperativa. No lleva forma de pago
/// (la cotización no cobra). Un borrador lleva la marca «BORRADOR» sin número; una anulada, «ANULADA». La sirve
/// <c>GET /sales/quotes/{id}/pdf</c> y la adjunta el envío al cliente (<see cref="SendSalesQuoteCommand"/>). (nuevo)
/// </summary>
public static class SalesQuoteReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(CotizacionImprimible c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var marca = c.Status switch
        {
            DocumentStatus.Draft => "BORRADOR — sin número",
            DocumentStatus.PendingApproval => "EN APROBACIÓN — sin número",
            DocumentStatus.Voided => $"ANULADA — {c.DisplayNumber}",
            DocumentStatus.Discarded => "DESCARTADA",
            _ => $"Número {c.DisplayNumber}",
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
                        row.RelativeItem().Column(e =>
                        {
                            e.Item().Text(c.Empresa.Name).Bold().FontSize(13);
                            if (!string.IsNullOrWhiteSpace(c.Empresa.TaxId)) e.Item().Text($"NIT {c.Empresa.TaxId}");
                            if (!string.IsNullOrWhiteSpace(c.Empresa.Address)) e.Item().Text(c.Empresa.Address);
                            var contacto = string.Join(" · ", new[] { c.Empresa.City, c.Empresa.Phone }.Where(x => !string.IsNullOrWhiteSpace(x)));
                            if (contacto.Length > 0) e.Item().Text(contacto).FontSize(8);
                        });
                        row.ConstantItem(220).AlignRight().Column(e =>
                        {
                            e.Item().Text("COTIZACIÓN").Bold().FontSize(13);
                            e.Item().Text(c.DocumentTypeName).FontSize(8);
                            e.Item().Text(marca).Bold().FontSize(11);
                            e.Item().Text($"Fecha {c.OperationDate.ToString("dd/MM/yyyy", Co)}");
                            if (c.ValidUntil is { } vence) e.Item().Text($"Vigente hasta {vence.ToString("dd/MM/yyyy", Co)}").SemiBold();
                        });
                    });
                    col.Item().PaddingTop(4).PaddingBottom(4).LineHorizontal(1);
                });

                page.Content().PaddingVertical(4).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(e =>
                        {
                            e.Item().Text("Cliente").Bold();
                            e.Item().Text(c.Cliente.Name);
                            if (!string.IsNullOrWhiteSpace(c.Cliente.IdNumber))
                            {
                                var digito = string.IsNullOrWhiteSpace(c.Cliente.CheckDigit) ? string.Empty : $"-{c.Cliente.CheckDigit}";
                                e.Item().Text($"{(string.IsNullOrWhiteSpace(c.Cliente.IdType) ? "Identificación" : c.Cliente.IdType)} {c.Cliente.IdNumber}{digito}");
                            }
                            if (!string.IsNullOrWhiteSpace(c.Cliente.Address)) e.Item().Text(c.Cliente.Address);
                            var contacto = string.Join(" · ", new[] { c.Cliente.Email, c.Cliente.Phone }.Where(x => !string.IsNullOrWhiteSpace(x)));
                            if (contacto.Length > 0) e.Item().Text(contacto).FontSize(8);
                        });
                        row.ConstantItem(20);
                        row.RelativeItem().Column(e =>
                        {
                            if (!string.IsNullOrWhiteSpace(c.Vendedor))
                            {
                                e.Item().Text("Vendedor").Bold();
                                e.Item().Text(c.Vendedor);
                            }
                        });
                    });

                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(20);   // #
                            cols.ConstantColumn(55);   // código
                            cols.RelativeColumn(3);    // descripción
                            cols.ConstantColumn(35);   // unidad
                            cols.ConstantColumn(45);   // cantidad
                            cols.ConstantColumn(62);   // precio
                            cols.ConstantColumn(55);   // descuento
                            cols.ConstantColumn(55);   // impuesto
                            cols.ConstantColumn(68);   // total
                        });
                        table.Header(h =>
                        {
                            foreach (var (titulo, derecha) in new[] { ("#", false), ("Código", false), ("Descripción", false), ("Unidad", false),
                                         ("Cantidad", true), ("Precio", true), ("Descuento", true), ("Impuesto", true), ("Total", true) })
                            {
                                var celda = h.Cell().BorderBottom(1).Padding(2);
                                if (derecha) celda.AlignRight().Text(titulo).Bold(); else celda.Text(titulo).Bold();
                            }
                        });
                        foreach (var l in c.Lineas)
                        {
                            table.Cell().Padding(2).Text(l.LineNumber.ToString(Co));
                            table.Cell().Padding(2).Text(l.ProductCode);
                            table.Cell().Padding(2).Text(l.Description);
                            table.Cell().Padding(2).Text(l.UnitCode);
                            table.Cell().Padding(2).AlignRight().Text(l.Quantity.ToString("0.####", Co));
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.UnitPrice));
                            table.Cell().Padding(2).AlignRight().Text(l.DiscountAmount > 0m ? Pesos(l.DiscountAmount) : string.Empty);
                            table.Cell().Padding(2).AlignRight().Text(l.TaxAmount > 0m ? Pesos(l.TaxAmount) : string.Empty);
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.Total));
                        }
                    });

                    col.Item().PaddingTop(6).AlignRight().Width(280).Column(t =>
                    {
                        Total(t, "Subtotal", c.Subtotal, false);
                        if (c.DiscountTotal > 0m) Total(t, "Descuentos", -c.DiscountTotal, false);
                        foreach (var i in c.Impuestos) Total(t, Renglon(i), i.Amount, false);
                        if (c.Impuestos.Count == 0 && c.TaxTotal != 0m) Total(t, "Impuestos", c.TaxTotal, false);
                        Total(t, "Total", c.Total, true);
                        foreach (var r in c.Retenciones) Total(t, Renglon(r), -r.Amount, false);
                        if (c.Retenciones.Count == 0 && c.WithholdingTotal != 0m) Total(t, "Retenciones", -c.WithholdingTotal, false);
                        if (c.WithholdingTotal != 0m) Total(t, "Valor a pagar", c.AmountDue, true);
                    });

                    if (!string.IsNullOrWhiteSpace(c.Notas))
                    {
                        col.Item().PaddingTop(10).Text("Notas").Bold();
                        col.Item().Text(c.Notas);
                    }
                    if (!string.IsNullOrWhiteSpace(c.Condiciones))
                    {
                        col.Item().PaddingTop(10).Text("Condiciones de la cotización").Bold();
                        col.Item().Text(c.Condiciones).FontSize(8);
                    }
                    col.Item().PaddingTop(10).Text(c.ValidUntil is { } hasta
                            ? $"Los precios de esta cotización se respetan hasta el {hasta.ToString("dd/MM/yyyy", Co)}. Esta cotización no es una factura."
                            : "Esta cotización no es una factura.")
                        .FontSize(7).Italic();

                    col.Item().PaddingTop(24).Row(row =>
                    {
                        Firma(row, "Elaboró", c.ElaboradaPor ?? string.Empty);
                        row.ConstantItem(30);
                        Firma(row, "Cliente (recibido)", string.Empty);
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"Cotización {c.DisplayNumber ?? "(borrador)"} · impresa el {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · página ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" de ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static string Renglon(RenglonDeLaCotizacion r)
    {
        var tarifa = r.Rate is { } t ? $" {(t * 100m).ToString("0.##", Co)} %" : string.Empty;
        return $"{r.Label}{tarifa} sobre {Pesos(r.Base)}";
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

/// <summary><see cref="ICotizacionEnPdf"/> con QuestPDF. Registrado en la API, la única capa que conoce la librería. (nuevo)</summary>
public sealed class SalesQuotePdfRenderer : ICotizacionEnPdf
{
    public byte[] Generar(CotizacionImprimible cotizacion) => SalesQuoteReport.Generate(cotizacion);
}
