using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record VoucherItemDto(
    string AccountCode,
    string AccountName,
    string ThirdPartyNit,
    string ThirdPartyName,
    string Description,
    decimal Debit,
    decimal Credit);

public record VoucherPrintDto(
    string VoucherType,
    string VoucherNumber,
    DateTime EntryDate,
    string CompanyName,
    string CompanyNit,
    string Description,
    List<VoucherItemDto> Items,
    decimal TotalDebit,
    decimal TotalCredit,
    string PreparedBy,
    string ApprovedBy);

public static class VoucherPrintReport
{
    public static byte[] Generate(VoucherPrintDto data)
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
                    col.Item().PaddingTop(5).Text($"COMPROBANTE {data.VoucherType}").FontSize(12).SemiBold();
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Numero: {data.VoucherNumber}").Bold();
                        row.ConstantItem(150).AlignRight().Text($"Fecha: {data.EntryDate:dd/MM/yyyy}");
                    });
                    col.Item().Text($"Concepto: {data.Description}").FontSize(9);
                    col.Item().PaddingBottom(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(5).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(70);   // Code
                            cols.RelativeColumn(2);     // Account
                            cols.ConstantColumn(80);   // NIT
                            cols.RelativeColumn(2);     // Description
                            cols.ConstantColumn(90);   // Debit
                            cols.ConstantColumn(90);   // Credit
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(2).Text("Codigo").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Cuenta").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("NIT").Bold();
                            header.Cell().BorderBottom(1).Padding(2).Text("Descripcion").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Debito").Bold();
                            header.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Credito").Bold();
                        });

                        foreach (var item in data.Items)
                        {
                            table.Cell().Padding(2).Text(item.AccountCode).FontSize(8);
                            table.Cell().Padding(2).Text(item.AccountName).FontSize(8);
                            table.Cell().Padding(2).Text(item.ThirdPartyNit).FontSize(8);
                            table.Cell().Padding(2).Text(item.Description).FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text(item.Debit > 0 ? $"{item.Debit:N2}" : "").FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text(item.Credit > 0 ? $"{item.Credit:N2}" : "").FontSize(8);
                        }
                    });

                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("TOTALES").Bold();
                        row.ConstantItem(90).AlignRight().Text($"{data.TotalDebit:N2}").Bold();
                        row.ConstantItem(90).AlignRight().Text($"{data.TotalCredit:N2}").Bold();
                    });

                    // Signature section
                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().AlignCenter().Text("Elaboro").FontSize(8);
                            c.Item().AlignCenter().Text(data.PreparedBy).FontSize(8);
                        });
                        row.ConstantItem(30);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().AlignCenter().Text("Aprobo").FontSize(8);
                            c.Item().AlignCenter().Text(data.ApprovedBy).FontSize(8);
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
