using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record SavingsMovementDto(
    DateTime Date,
    string TransactionCode,
    string Description,
    decimal Deposit,
    decimal Withdrawal,
    decimal Balance);

public record SavingsStatementDto(
    string CompanyName,
    string CompanyNit,
    string PersonName,
    string PersonNit,
    string AccountType,
    string AccountNumber,
    DateTime DateFrom,
    DateTime DateTo,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalDeposits,
    decimal TotalWithdrawals,
    List<SavingsMovementDto> Movements);

public static class SavingsStatementReport
{
    public static byte[] Generate(SavingsStatementDto data)
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
                    col.Item().PaddingTop(5).Text("EXTRACTO DE AHORROS").FontSize(12).SemiBold();
                    col.Item().Text($"Del {data.DateFrom:dd/MM/yyyy} al {data.DateTo:dd/MM/yyyy}").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    // Account info
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Titular: {data.PersonName}").SemiBold();
                            c.Item().Text($"NIT/CC: {data.PersonNit}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Tipo: {data.AccountType}").SemiBold();
                            c.Item().Text($"Numero: {data.AccountNumber}");
                        });
                    });

                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"Saldo Anterior: {data.OpeningBalance:N2}");
                        row.RelativeItem().Text($"Saldo Actual: {data.ClosingBalance:N2}").Bold();
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                    // Movements
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(70);   // Date
                            cols.ConstantColumn(60);   // Code
                            cols.RelativeColumn();      // Description
                            cols.ConstantColumn(100);  // Deposit
                            cols.ConstantColumn(100);  // Withdrawal
                            cols.ConstantColumn(100);  // Balance
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(2).Text("Fecha").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Codigo").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Descripcion").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Deposito").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Retiro").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Saldo").Bold();
                        });

                        foreach (var mov in data.Movements)
                        {
                            table.Cell().Padding(2).Text(mov.Date.ToString("dd/MM/yyyy")).FontSize(8);
                            table.Cell().Padding(2).Text(mov.TransactionCode).FontSize(8);
                            table.Cell().Padding(2).Text(mov.Description).FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text(mov.Deposit > 0 ? $"{mov.Deposit:N2}" : "").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text(mov.Withdrawal > 0 ? $"{mov.Withdrawal:N2}" : "").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text($"{mov.Balance:N2}").FontSize(8);
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("TOTALES").Bold();
                        row.ConstantItem(100).AlignRight().Text($"{data.TotalDeposits:N2}").Bold();
                        row.ConstantItem(100).AlignRight().Text($"{data.TotalWithdrawals:N2}").Bold();
                        row.ConstantItem(100).AlignRight().Text($"{data.ClosingBalance:N2}").Bold();
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
