using IngenIA365ERP.Application.Accounting.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

public static class GeneralLedgerReport
{
    public static byte[] Generate(GeneralLedgerDto data)
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
                    col.Item().Text("IngenIA365ERP").Bold().FontSize(14);
                    col.Item().Text("Libro Mayor y Balances").FontSize(12).SemiBold();
                    col.Item().Text($"Del {data.DateFrom:dd/MM/yyyy} al {data.DateTo:dd/MM/yyyy} | Generado: {DateTime.Now:dd/MM/yyyy}").FontSize(8);
                    if (!string.IsNullOrEmpty(data.AccountCodeFrom) || !string.IsNullOrEmpty(data.AccountCodeTo))
                        col.Item().Text($"Cuentas: {data.AccountCodeFrom ?? "Inicio"} - {data.AccountCodeTo ?? "Fin"}").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    foreach (var acct in data.Accounts)
                    {
                        col.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem().Text($"{acct.AccountCode} - {acct.AccountName}").Bold().FontSize(9);
                            row.ConstantItem(150).AlignRight().Text($"Saldo Anterior: {acct.OpeningBalance:N2}").FontSize(8);
                        });

                        if (acct.Movements.Count > 0)
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(70);   // Date
                                    cols.ConstantColumn(50);   // VoucherType
                                    cols.ConstantColumn(60);   // VoucherNumber
                                    cols.RelativeColumn();      // Description
                                    cols.ConstantColumn(80);   // NIT
                                    cols.ConstantColumn(90);   // Debit
                                    cols.ConstantColumn(90);   // Credit
                                    cols.ConstantColumn(100);  // Balance
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(0.5f).Padding(2).Text("Fecha").Bold();
                                    header.Cell().BorderBottom(0.5f).Padding(2).Text("Tipo").Bold();
                                    header.Cell().BorderBottom(0.5f).Padding(2).Text("Numero").Bold();
                                    header.Cell().BorderBottom(0.5f).Padding(2).Text("Descripcion").Bold();
                                    header.Cell().BorderBottom(0.5f).Padding(2).Text("NIT").Bold();
                                    header.Cell().BorderBottom(0.5f).Padding(2).AlignRight().Text("Debito").Bold();
                                    header.Cell().BorderBottom(0.5f).Padding(2).AlignRight().Text("Credito").Bold();
                                    header.Cell().BorderBottom(0.5f).Padding(2).AlignRight().Text("Saldo").Bold();
                                });

                                foreach (var mov in acct.Movements)
                                {
                                    table.Cell().Padding(1).Text(mov.Date.ToString("dd/MM/yyyy"));
                                    table.Cell().Padding(1).Text(mov.VoucherType);
                                    table.Cell().Padding(1).Text(mov.DocumentNumber);
                                    table.Cell().Padding(1).Text(mov.Description);
                                    table.Cell().Padding(1).Text(mov.ThirdPartyTaxId);
                                    table.Cell().Padding(1).AlignRight().Text($"{mov.Debit:N2}");
                                    table.Cell().Padding(1).AlignRight().Text($"{mov.Credit:N2}");
                                    table.Cell().Padding(1).AlignRight().Text($"{mov.RunningBalance:N2}");
                                }
                            });
                        }

                        col.Item().BorderTop(0.5f).PaddingTop(2).Row(row =>
                        {
                            row.RelativeItem().Text("Totales Cuenta").Bold();
                            row.ConstantItem(90).AlignRight().Text($"{acct.TotalDebit:N2}").Bold();
                            row.ConstantItem(90).AlignRight().Text($"{acct.TotalCredit:N2}").Bold();
                            row.ConstantItem(100).AlignRight().Text($"{acct.ClosingBalance:N2}").Bold();
                        });
                    }
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
