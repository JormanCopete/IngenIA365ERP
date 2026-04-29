using IngenIA365ERP.Application.Lending.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

public static class PortfolioAgingReport
{
    public static byte[] Generate(PortfolioAgingDto data)
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
                    col.Item().Text("Cartera por Edades").FontSize(12).SemiBold();
                    col.Item().Text($"Corte: {data.AsOfDate:dd/MM/yyyy} | Generado: {DateTime.Now:dd/MM/yyyy}").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Summary
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total Prestamos: {data.TotalLoans}").SemiBold();
                        row.RelativeItem().Text($"Saldo Total: {data.GrandTotalBalance:N2}").SemiBold();
                        row.RelativeItem().Text($"Total en Mora: {data.GrandTotalOverdue:N2}").SemiBold();
                    });

                    col.Item().PaddingVertical(8);

                    // Aging table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();      // Bucket label
                            cols.ConstantColumn(80);   // Count
                            cols.ConstantColumn(120);  // Balance
                            cols.ConstantColumn(120);  // Overdue
                            cols.ConstantColumn(80);   // Percent
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(3).Text("Rango de Mora").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Obligaciones").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Saldo Capital").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Valor en Mora").Bold();
                            header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("% del Total").Bold();
                        });

                        foreach (var bucket in data.Buckets)
                        {
                            table.Cell().Padding(3).Text(bucket.BucketLabel);
                            table.Cell().Padding(3).AlignRight().Text($"{bucket.LoanCount}");
                            table.Cell().Padding(3).AlignRight().Text($"{bucket.TotalBalance:N2}");
                            table.Cell().Padding(3).AlignRight().Text($"{bucket.TotalOverdue:N2}");
                            table.Cell().Padding(3).AlignRight().Text($"{bucket.PercentOfTotal:N2}%");
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("TOTALES").Bold();
                        row.ConstantItem(80).AlignRight().Text($"{data.TotalLoans}").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.GrandTotalBalance:N2}").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.GrandTotalOverdue:N2}").Bold();
                        row.ConstantItem(80).AlignRight().Text("100.00%").Bold();
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
