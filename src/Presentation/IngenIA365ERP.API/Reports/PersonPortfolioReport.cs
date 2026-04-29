using IngenIA365ERP.Application.Lending.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

public static class PersonPortfolioReport
{
    public static byte[] Generate(PersonPortfolioDto data)
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
                    col.Item().Text("Portafolio del Asociado").FontSize(12).SemiBold();
                    col.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy}").FontSize(8);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    // Person info
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(100).Text("Asociado:").SemiBold();
                        row.RelativeItem().Text(data.PersonName);
                    });
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(100).Text("Identificacion:").SemiBold();
                        row.RelativeItem().Text(data.TaxId);
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                    // Loans
                    if (data.Loans.Count > 0)
                    {
                        col.Item().Text("CREDITOS").Bold().FontSize(10);
                        col.Item().PaddingBottom(5).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();      // Line
                                cols.ConstantColumn(100);  // Approved
                                cols.ConstantColumn(100);  // Balance
                                cols.ConstantColumn(80);   // Overdue
                                cols.ConstantColumn(60);   // Status
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).Padding(2).Text("Linea de Credito").Bold();
                                header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Aprobado").Bold();
                                header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Saldo").Bold();
                                header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Mora").Bold();
                                header.Cell().BorderBottom(1).Padding(2).Text("Estado").Bold();
                            });

                            foreach (var loan in data.Loans)
                            {
                                table.Cell().Padding(2).Text($"{loan.PortfolioNumber} - {loan.CreditLineName}");
                                table.Cell().Padding(2).AlignRight().Text($"{loan.ApprovedAmount:N2}");
                                table.Cell().Padding(2).AlignRight().Text($"{loan.CurrentBalance:N2}");
                                table.Cell().Padding(2).AlignRight().Text($"{loan.OverdueAmount:N2}");
                                table.Cell().Padding(2).Text(loan.Status);
                            }
                        });
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Total Deuda").Bold();
                            row.ConstantItem(100).AlignRight().Text($"{data.TotalDebt:N2}").Bold();
                        });
                    }

                    col.Item().PaddingVertical(5);

                    // Savings
                    if (data.Savings.Count > 0)
                    {
                        col.Item().Text("AHORROS").Bold().FontSize(10);
                        col.Item().PaddingBottom(5).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.ConstantColumn(120);
                                cols.ConstantColumn(120);
                                cols.ConstantColumn(60);
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).Padding(2).Text("Tipo").Bold();
                                header.Cell().BorderBottom(1).Padding(2).Text("Numero").Bold();
                                header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Saldo").Bold();
                                header.Cell().BorderBottom(1).Padding(2).Text("Estado").Bold();
                            });

                            foreach (var sav in data.Savings)
                            {
                                table.Cell().Padding(2).Text(sav.SavingsLineId);
                                table.Cell().Padding(2).Text(sav.AccountNumber);
                                table.Cell().Padding(2).AlignRight().Text($"{sav.Balance:N2}");
                                table.Cell().Padding(2).Text(sav.Status);
                            }
                        });
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Total Ahorros").Bold();
                            row.ConstantItem(120).AlignRight().Text($"{data.TotalSavings:N2}").Bold();
                        });
                    }

                    col.Item().PaddingVertical(5);

                    // CDTs
                    if (data.Cdts.Count > 0)
                    {
                        col.Item().Text("CDTS").Bold().FontSize(10);
                        col.Item().PaddingBottom(5).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(100);
                                cols.ConstantColumn(100);
                                cols.ConstantColumn(60);
                                cols.ConstantColumn(60);
                                cols.ConstantColumn(80);
                                cols.ConstantColumn(60);
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).Padding(2).Text("Numero").Bold();
                                header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Monto").Bold();
                                header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Tasa %").Bold();
                                header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Plazo").Bold();
                                header.Cell().BorderBottom(1).Padding(2).Text("Vencimiento").Bold();
                                header.Cell().BorderBottom(1).Padding(2).Text("Estado").Bold();
                            });

                            foreach (var cdt in data.Cdts)
                            {
                                table.Cell().Padding(2).Text(cdt.CertificateNumber);
                                table.Cell().Padding(2).AlignRight().Text($"{cdt.Amount:N2}");
                                table.Cell().Padding(2).AlignRight().Text($"{cdt.InterestRate:N2}");
                                table.Cell().Padding(2).AlignRight().Text($"{cdt.Term} dias");
                                table.Cell().Padding(2).Text(cdt.MaturityDate?.ToString("dd/MM/yyyy") ?? "");
                                table.Cell().Padding(2).Text(cdt.Status);
                            }
                        });
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Total CDTs").Bold();
                            row.ConstantItem(100).AlignRight().Text($"{data.TotalCdts:N2}").Bold();
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
