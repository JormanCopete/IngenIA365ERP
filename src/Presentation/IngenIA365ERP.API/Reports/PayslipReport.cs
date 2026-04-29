using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record PayslipConceptDto(
    string Code,
    string Name,
    decimal Earned,
    decimal Deducted);

public record PayslipDto(
    string CompanyName,
    string CompanyNit,
    string EmployeeName,
    string EmployeeNit,
    string Position,
    string Department,
    decimal BaseSalary,
    string PeriodName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<PayslipConceptDto> Concepts,
    decimal TotalEarned,
    decimal TotalDeducted,
    decimal NetPay,
    int WorkedDays);

public static class PayslipReport
{
    public static byte[] Generate(PayslipDto data)
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
                    col.Item().Text(data.CompanyName).Bold().FontSize(14);
                    col.Item().Text($"NIT: {data.CompanyNit}").FontSize(8);
                    col.Item().PaddingTop(5).AlignCenter().Text("COMPROBANTE DE NOMINA").FontSize(12).SemiBold();
                    col.Item().AlignCenter().Text($"Periodo: {data.PeriodName} ({data.PeriodStart:dd/MM/yyyy} - {data.PeriodEnd:dd/MM/yyyy})").FontSize(9);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    // Employee info
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Empleado: {data.EmployeeName}").SemiBold();
                            c.Item().Text($"NIT/CC: {data.EmployeeNit}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Cargo: {data.Position}");
                            c.Item().Text($"Departamento: {data.Department}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Salario Base: {data.BaseSalary:N2}");
                            c.Item().Text($"Dias Trabajados: {data.WorkedDays}");
                        });
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                    // Concepts table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(50);   // Code
                            cols.RelativeColumn();      // Concept
                            cols.ConstantColumn(120);  // Earned
                            cols.ConstantColumn(120);  // Deducted
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(2).Text("Codigo").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Concepto").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Devengado").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Deduccion").Bold();
                        });

                        foreach (var concept in data.Concepts)
                        {
                            table.Cell().Padding(2).Text(concept.Code).FontSize(8);
                            table.Cell().Padding(2).Text(concept.Name).FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text(concept.Earned > 0 ? $"{concept.Earned:N2}" : "").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text(concept.Deducted > 0 ? $"{concept.Deducted:N2}" : "").FontSize(8);
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("TOTALES").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.TotalEarned:N2}").Bold();
                        row.ConstantItem(120).AlignRight().Text($"{data.TotalDeducted:N2}").Bold();
                    });

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Text("NETO A PAGAR").Bold().FontSize(12);
                        row.ConstantItem(150).AlignRight().Text($"${data.NetPay:N2}").Bold().FontSize(12);
                    });

                    // Signatures
                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().AlignCenter().Text("Empleador").FontSize(8);
                        });
                        row.ConstantItem(30);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().AlignCenter().Text("Empleado").FontSize(8);
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
