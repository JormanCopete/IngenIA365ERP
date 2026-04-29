using IngenIA365ERP.Application.Accounting.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

public static class BalanceSheetReport
{
    public static byte[] Generate(BalanceSheetDto data)
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
                    col.Item().Text("Balance General").FontSize(12).SemiBold();
                    col.Item().Text($"Periodo: {data.Year}-{data.Month:D2} | Fecha: {DateTime.Now:dd/MM/yyyy}").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Assets section
                    col.Item().Text("ACTIVOS").Bold().FontSize(10);
                    col.Item().PaddingBottom(5).Element(c => BuildAccountTable(c, data.Assets));
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Total Activos").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.TotalAssets:N2}").Bold();
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                    // Liabilities section
                    col.Item().Text("PASIVOS").Bold().FontSize(10);
                    col.Item().PaddingBottom(5).Element(c => BuildAccountTable(c, data.Liabilities));
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Total Pasivos").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.TotalLiabilities:N2}").Bold();
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                    // Equity section
                    col.Item().Text("PATRIMONIO").Bold().FontSize(10);
                    col.Item().PaddingBottom(5).Element(c => BuildAccountTable(c, data.Equity));
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Total Patrimonio").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.TotalEquity:N2}").Bold();
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1);

                    // Balance check
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL PASIVO + PATRIMONIO").Bold().FontSize(10);
                        row.ConstantItem(120).AlignRight()
                            .Text($"{data.TotalLiabilities + data.TotalEquity:N2}").Bold().FontSize(10);
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

    private static void BuildAccountTable(IContainer container, List<BalanceSheetLineDto> lines)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(80);  // Code
                cols.RelativeColumn();     // Name
                cols.ConstantColumn(120);  // Balance
            });

            table.Header(header =>
            {
                header.Cell().BorderBottom(1).Padding(2).Text("Codigo").Bold().FontSize(8);
                header.Cell().BorderBottom(1).Padding(2).Text("Cuenta").Bold().FontSize(8);
                header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Saldo").Bold().FontSize(8);
            });

            foreach (var line in lines)
            {
                var indent = (line.Level - 1) * 10;
                var isBold = line.Level <= 2;

                table.Cell().Padding(2).Text(line.AccountCode).FontSize(8);
                if (isBold)
                    table.Cell().Padding(2).PaddingLeft(indent).Text(line.AccountName).FontSize(8).Bold();
                else
                    table.Cell().Padding(2).PaddingLeft(indent).Text(line.AccountName).FontSize(8);
                table.Cell().Padding(2).AlignRight().Text($"{line.Balance:N2}").FontSize(8);
            }
        });
    }
}
