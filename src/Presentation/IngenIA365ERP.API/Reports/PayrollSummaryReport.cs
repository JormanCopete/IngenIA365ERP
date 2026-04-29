using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record PayrollSummaryLineDto(
    string EmployeeNit,
    string EmployeeName,
    string Position,
    int WorkedDays,
    decimal BaseSalary,
    decimal TotalEarned,
    decimal TotalDeducted,
    decimal NetPay);

public record PayrollSummaryDto(
    string CompanyName,
    string CompanyNit,
    string PeriodName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<PayrollSummaryLineDto> Lines,
    decimal GrandTotalEarned,
    decimal GrandTotalDeducted,
    decimal GrandTotalNetPay,
    int TotalEmployees);

public static class PayrollSummaryReport
{
    public static byte[] Generate(PayrollSummaryDto data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text(data.CompanyName).Bold().FontSize(14);
                    col.Item().Text($"NIT: {data.CompanyNit}").FontSize(8);
                    col.Item().PaddingTop(3).Text("RESUMEN DE NOMINA").FontSize(12).SemiBold();
                    col.Item().Text($"Periodo: {data.PeriodName} ({data.PeriodStart:dd/MM/yyyy} - {data.PeriodEnd:dd/MM/yyyy})").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(80);   // NIT
                            cols.RelativeColumn(2);     // Name
                            cols.RelativeColumn();      // Position
                            cols.ConstantColumn(40);   // Days
                            cols.ConstantColumn(90);   // Salary
                            cols.ConstantColumn(100);  // Earned
                            cols.ConstantColumn(100);  // Deducted
                            cols.ConstantColumn(100);  // Net
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(2).Text("NIT/CC").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Empleado").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Cargo").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Dias").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Salario").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Devengado").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Deduccion").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Neto").Bold();
                        });

                        foreach (var line in data.Lines)
                        {
                            table.Cell().Padding(1).Text(line.EmployeeNit).FontSize(7);
                            table.Cell().Padding(1).Text(line.EmployeeName).FontSize(7);
                            table.Cell().Padding(1).Text(line.Position).FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{line.WorkedDays}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{line.BaseSalary:N2}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{line.TotalEarned:N2}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{line.TotalDeducted:N2}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{line.NetPay:N2}").FontSize(7);
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text($"TOTALES ({data.TotalEmployees} empleados)").Bold();
                        row.ConstantItem(100).AlignRight().Text($"{data.GrandTotalEarned:N2}").Bold();
                        row.ConstantItem(100).AlignRight().Text($"{data.GrandTotalDeducted:N2}").Bold();
                        row.ConstantItem(100).AlignRight().Text($"{data.GrandTotalNetPay:N2}").Bold();
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
