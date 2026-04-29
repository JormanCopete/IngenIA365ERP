using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record CdtCertificateDto(
    string CompanyName,
    string CompanyNit,
    string CompanyAddress,
    string PersonName,
    string PersonNit,
    string CertificateNumber,
    decimal Amount,
    decimal Rate,
    int TermDays,
    DateTime IssueDate,
    DateTime MaturityDate,
    string PaymentFrequency,
    decimal ExpectedInterest,
    string AccountForPayment);

public static class CdtCertificateReport
{
    public static byte[] Generate(CdtCertificateDto data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(data.CompanyName).Bold().FontSize(16);
                    col.Item().AlignCenter().Text($"NIT: {data.CompanyNit}").FontSize(10);
                    col.Item().AlignCenter().Text(data.CompanyAddress).FontSize(8);
                    col.Item().PaddingTop(15).AlignCenter().Text("CERTIFICADO DE DEPOSITO A TERMINO").Bold().FontSize(14);
                    col.Item().AlignCenter().Text($"No. {data.CertificateNumber}").Bold().FontSize(12);
                    col.Item().PaddingBottom(10).PaddingTop(5).LineHorizontal(2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Certificate details in a formal layout
                    col.Item().PaddingBottom(15).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(160);
                            cols.RelativeColumn();
                        });

                        AddRow(table, "Titular:", data.PersonName);
                        AddRow(table, "Identificacion:", data.PersonNit);
                        AddRow(table, "Valor del CDT:", $"${data.Amount:N2}");
                        AddRow(table, "Tasa Efectiva Anual:", $"{data.Rate:N2}%");
                        AddRow(table, "Plazo:", $"{data.TermDays} dias");
                        AddRow(table, "Fecha de Constitucion:", data.IssueDate.ToString("dd/MM/yyyy"));
                        AddRow(table, "Fecha de Vencimiento:", data.MaturityDate.ToString("dd/MM/yyyy"));
                        AddRow(table, "Periodicidad Pago:", data.PaymentFrequency);
                        AddRow(table, "Intereses Esperados:", $"${data.ExpectedInterest:N2}");
                        AddRow(table, "Cuenta de Pago:", data.AccountForPayment);
                    });

                    col.Item().PaddingTop(10).Text(
                        $"La {data.CompanyName} certifica que el titular arriba mencionado ha constituido " +
                        $"un Certificado de Deposito a Termino por valor de ${data.Amount:N2} " +
                        $"(pesos colombianos), a una tasa efectiva anual del {data.Rate:N2}%, " +
                        $"con vencimiento el {data.MaturityDate:dd/MM/yyyy}.")
                        .FontSize(10).LineHeight(1.4f);

                    col.Item().PaddingTop(10).Text(
                        "Este certificado es un titulo valor nominativo y se rige por las disposiciones " +
                        "legales vigentes y el reglamento de captaciones de la entidad.")
                        .FontSize(9).Italic();

                    // Signatures
                    col.Item().PaddingTop(50).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().AlignCenter().Text("Gerente General").Bold().FontSize(9);
                        });
                        row.ConstantItem(50);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f);
                            c.Item().AlignCenter().Text("Titular / Beneficiario").Bold().FontSize(9);
                        });
                    });
                });

                page.Footer().AlignCenter().Text($"Expedido: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7);
            });
        });

        return document.GeneratePdf();
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(3).Text(label).SemiBold();
        table.Cell().Padding(3).Text(value);
    }
}
