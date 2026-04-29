using IngenIA365ERP.Application.Accounting.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

public static class IncomeStatementReport
{
    public static byte[] Generate(IncomeStatementDto data)
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
                    col.Item().Text("Estado de Resultados").FontSize(12).SemiBold();
                    col.Item().Text($"Periodo: {data.Year}-{data.Month:D2} | Fecha: {DateTime.Now:dd/MM/yyyy}").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Income
                    col.Item().Text("INGRESOS").Bold().FontSize(10);
                    col.Item().PaddingBottom(5).Element(c => BuildSection(c, data.Income));
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Total Ingresos").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.TotalIncome:N2}").Bold();
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                    // Expenses
                    col.Item().Text("GASTOS Y COSTOS").Bold().FontSize(10);
                    col.Item().PaddingBottom(5).Element(c => BuildSection(c, data.Expenses));
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Total Gastos y Costos").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.TotalExpenses:N2}").Bold();
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1);

                    // Net profit/loss
                    var isProfit = data.NetProfit >= 0;
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(isProfit ? "UTILIDAD DEL EJERCICIO" : "PERDIDA DEL EJERCICIO")
                            .Bold().FontSize(11);
                        row.ConstantItem(120).AlignRight()
                            .Text($"{data.NetProfit:N2}").Bold().FontSize(11);
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

    private static void BuildSection(IContainer container, List<IncomeStatementLineDto> lines)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(80);
                cols.RelativeColumn();
                cols.ConstantColumn(120);
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
                table.Cell().Padding(2).AlignRight().Text($"{line.Amount:N2}").FontSize(8);
            }
        });
    }
}
