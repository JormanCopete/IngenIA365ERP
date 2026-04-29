using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record LoanInstallmentDto(
    int Number,
    DateTime DueDate,
    decimal Capital,
    decimal Interest,
    decimal Insurance,
    decimal OtherCharges,
    decimal Total,
    decimal PaidAmount,
    decimal Balance,
    string Status);

public record LoanStatementDto(
    string CompanyName,
    string CompanyNit,
    string PersonName,
    string PersonNit,
    string CreditLineCode,
    string CreditLineName,
    string LoanNumber,
    decimal ApprovedAmount,
    DateTime DisbursementDate,
    decimal AnnualRate,
    int TotalInstallments,
    decimal CurrentBalance,
    decimal OverdueAmount,
    List<LoanInstallmentDto> Installments);

public static class LoanStatementReport
{
    public static byte[] Generate(LoanStatementDto data)
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
                    col.Item().PaddingTop(5).Text("EXTRACTO DE CREDITO").FontSize(12).SemiBold();
                    col.Item().Text($"Fecha de generacion: {DateTime.Now:dd/MM/yyyy}").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    // Borrower info
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Asociado: {data.PersonName}").SemiBold();
                            c.Item().Text($"NIT/CC: {data.PersonNit}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Linea: {data.CreditLineCode} - {data.CreditLineName}").SemiBold();
                            c.Item().Text($"Obligacion: {data.LoanNumber}");
                        });
                    });

                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"Monto aprobado: {data.ApprovedAmount:N2}");
                        row.RelativeItem().Text($"Desembolso: {data.DisbursementDate:dd/MM/yyyy}");
                        row.RelativeItem().Text($"Tasa: {data.AnnualRate:N2}% E.A.");
                        row.RelativeItem().Text($"Plazo: {data.TotalInstallments} cuotas");
                    });

                    col.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text($"Saldo actual: {data.CurrentBalance:N2}").Bold();
                        row.RelativeItem().Text($"Mora: {data.OverdueAmount:N2}").Bold();
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                    // Amortization table
                    col.Item().Text("PLAN DE AMORTIZACION").Bold().FontSize(10);
                    col.Item().PaddingTop(3).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(35);  // #
                            cols.ConstantColumn(70);  // Date
                            cols.ConstantColumn(80);  // Capital
                            cols.ConstantColumn(80);  // Interest
                            cols.ConstantColumn(65);  // Insurance
                            cols.ConstantColumn(70);  // Total
                            cols.ConstantColumn(80);  // Paid
                            cols.ConstantColumn(80);  // Balance
                            cols.ConstantColumn(55);  // Status
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(2).Text("#").Bold().FontSize(7);
                            header.Cell().BorderBottom(1).Padding(2).Text("Vencimiento").Bold().FontSize(7);
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Capital").Bold().FontSize(7);
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Interes").Bold().FontSize(7);
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Seguro").Bold().FontSize(7);
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Total").Bold().FontSize(7);
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Pagado").Bold().FontSize(7);
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Saldo").Bold().FontSize(7);
                            header.Cell().BorderBottom(1).Padding(2).Text("Estado").Bold().FontSize(7);
                        });

                        foreach (var inst in data.Installments)
                        {
                            table.Cell().Padding(1).Text($"{inst.Number}").FontSize(7);
                            table.Cell().Padding(1).Text(inst.DueDate.ToString("dd/MM/yyyy")).FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{inst.Capital:N2}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{inst.Interest:N2}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{inst.Insurance:N2}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{inst.Total:N2}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{inst.PaidAmount:N2}").FontSize(7);
                            table.Cell().Padding(1).AlignRight().Text($"{inst.Balance:N2}").FontSize(7);
                            table.Cell().Padding(1).Text(inst.Status).FontSize(7);
                        }
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
