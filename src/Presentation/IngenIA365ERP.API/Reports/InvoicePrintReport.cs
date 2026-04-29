using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record InvoiceItemDto(
    string ProductCode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal VatRate,
    decimal VatAmount,
    decimal Total);

public record InvoicePrintDto(
    string CompanyName,
    string CompanyNit,
    string CompanyAddress,
    string CompanyPhone,
    string Resolution,
    string InvoiceNumber,
    string Prefix,
    DateTime InvoiceDate,
    DateTime DueDate,
    string CustomerName,
    string CustomerNit,
    string CustomerAddress,
    string CustomerPhone,
    string CustomerCity,
    string PaymentMethod,
    List<InvoiceItemDto> Items,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalVat,
    decimal GrandTotal,
    string AmountInWords);

public static class InvoicePrintReport
{
    public static byte[] Generate(InvoicePrintDto data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(data.CompanyName).Bold().FontSize(14);
                            c.Item().Text($"NIT: {data.CompanyNit}").FontSize(9);
                            c.Item().Text(data.CompanyAddress).FontSize(8);
                            c.Item().Text($"Tel: {data.CompanyPhone}").FontSize(8);
                        });
                        row.ConstantItem(200).Column(c =>
                        {
                            c.Item().AlignRight().Text("FACTURA DE VENTA").Bold().FontSize(12);
                            c.Item().AlignRight().Text($"No. {data.Prefix}{data.InvoiceNumber}").Bold().FontSize(11);
                            c.Item().AlignRight().Text($"Fecha: {data.InvoiceDate:dd/MM/yyyy}").FontSize(9);
                            c.Item().AlignRight().Text($"Vence: {data.DueDate:dd/MM/yyyy}").FontSize(9);
                        });
                    });
                    col.Item().Text($"Resolucion DIAN: {data.Resolution}").FontSize(7).Italic();
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    // Customer info
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Cliente: {data.CustomerName}").SemiBold();
                            c.Item().Text($"NIT/CC: {data.CustomerNit}");
                            c.Item().Text($"Direccion: {data.CustomerAddress}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Ciudad: {data.CustomerCity}");
                            c.Item().Text($"Telefono: {data.CustomerPhone}");
                            c.Item().Text($"Forma de Pago: {data.PaymentMethod}");
                        });
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                    // Items table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(60);   // Code
                            cols.RelativeColumn();      // Product
                            cols.ConstantColumn(55);   // Qty
                            cols.ConstantColumn(80);   // Unit Price
                            cols.ConstantColumn(65);   // Discount
                            cols.ConstantColumn(50);   // IVA %
                            cols.ConstantColumn(70);   // IVA $
                            cols.ConstantColumn(90);   // Total
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(2).Text("Codigo").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Descripcion").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Cant.").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Vr. Unit.").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Dcto.").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("IVA %").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("IVA $").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Total").Bold();
                        });

                        foreach (var item in data.Items)
                        {
                            table.Cell().Padding(2).Text(item.ProductCode).FontSize(8);
                            table.Cell().Padding(2).Text(item.ProductName).FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{item.Quantity:N2}").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{item.UnitPrice:N2}").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{item.Discount:N2}").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{item.VatRate:N0}%").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{item.VatAmount:N2}").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{item.Total:N2}").FontSize(8);
                        }
                    });

                    // Totals
                    col.Item().BorderTop(1).PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Son: " + data.AmountInWords).FontSize(8).Italic();
                        });
                        row.ConstantItem(200).Column(c =>
                        {
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Subtotal:");
                                r.ConstantItem(90).AlignRight().Text($"{data.Subtotal:N2}");
                            });
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Descuento:");
                                r.ConstantItem(90).AlignRight().Text($"{data.TotalDiscount:N2}");
                            });
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("IVA:");
                                r.ConstantItem(90).AlignRight().Text($"{data.TotalVat:N2}");
                            });
                            c.Item().PaddingTop(3).BorderTop(1).Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("TOTAL:").Bold();
                                r.ConstantItem(90).AlignRight().Text($"{data.GrandTotal:N2}").Bold();
                            });
                        });
                    });

                    // Signatures
                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().AlignCenter().Text("Elaboro").FontSize(8);
                        });
                        row.ConstantItem(30);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().AlignCenter().Text("Recibi conforme").FontSize(8);
                        });
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
