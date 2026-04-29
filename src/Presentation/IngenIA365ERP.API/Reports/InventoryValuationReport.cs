using IngenIA365ERP.Application.Inventory.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

public static class InventoryValuationReport
{
    public static byte[] Generate(InventoryValuationDto data)
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
                    col.Item().Text("IngenIA365ERP").Bold().FontSize(14);
                    col.Item().Text("Inventario Valorizado").FontSize(12).SemiBold();
                    col.Item().Text($"Bodega: {data.WarehouseName ?? "Todas"} | Fecha: {data.AsOfDate:dd/MM/yyyy}").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total Productos: {data.TotalProducts}").SemiBold();
                        row.RelativeItem().AlignRight().Text($"Valor Total: ${data.GrandTotal:N2}").Bold();
                    });

                    col.Item().PaddingVertical(5);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(70);   // Code
                            cols.RelativeColumn(2);     // Product
                            cols.RelativeColumn();      // Group
                            cols.ConstantColumn(70);   // Stock
                            cols.ConstantColumn(90);   // Unit Cost
                            cols.ConstantColumn(100);  // Total
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(2).Text("Codigo").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Producto").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Grupo").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Cantidad").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Costo Unit.").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Costo Total").Bold();
                        });

                        foreach (var line in data.Lines)
                        {
                            table.Cell().Padding(2).Text(line.ProductCode).FontSize(8);
                            table.Cell().Padding(2).Text(line.ProductName).FontSize(8);
                            table.Cell().Padding(2).Text(line.GroupName).FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{line.Stock:N2}").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{line.UnitCost:N2}").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{line.TotalCost:N2}").FontSize(8);
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text($"TOTAL ({data.TotalProducts} productos)").Bold();
                        row.ConstantItem(100).AlignRight().Text($"${data.GrandTotal:N2}").Bold();
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
