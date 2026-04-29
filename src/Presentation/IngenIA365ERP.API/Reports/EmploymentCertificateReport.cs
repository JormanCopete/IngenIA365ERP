using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

// --- DTOs ---

public record EmploymentCertificateDto(
    string CompanyName,
    string CompanyNit,
    string CompanyAddress,
    string CompanyCity,
    string EmployeeName,
    string EmployeeNit,
    string Position,
    string Department,
    decimal CurrentSalary,
    DateTime HireDate,
    DateTime? TerminationDate,
    string ContractType,
    string SignatoryName,
    string SignatoryTitle);

public static class EmploymentCertificateReport
{
    public static byte[] Generate(EmploymentCertificateDto data)
    {
        var isActive = !data.TerminationDate.HasValue;
        var tenure = isActive
            ? $"desde el {data.HireDate:dd} de {MonthName(data.HireDate.Month)} de {data.HireDate.Year}"
            : $"desde el {data.HireDate:dd} de {MonthName(data.HireDate.Month)} de {data.HireDate.Year} " +
              $"hasta el {data.TerminationDate!.Value:dd} de {MonthName(data.TerminationDate.Value.Month)} de {data.TerminationDate.Value.Year}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(60);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(data.CompanyName).Bold().FontSize(16);
                    col.Item().AlignCenter().Text($"NIT: {data.CompanyNit}").FontSize(10);
                    col.Item().AlignCenter().Text(data.CompanyAddress).FontSize(9);
                    col.Item().AlignCenter().Text(data.CompanyCity).FontSize(9);
                });

                page.Content().PaddingVertical(30).Column(col =>
                {
                    col.Item().AlignCenter().PaddingBottom(20).Text("CERTIFICACION LABORAL").Bold().FontSize(14);

                    col.Item().Text($"{data.CompanyCity}, {DateTime.Now:dd} de {MonthName(DateTime.Now.Month)} de {DateTime.Now.Year}").FontSize(10);

                    col.Item().PaddingTop(20).Text("A QUIEN INTERESE:").Bold();

                    col.Item().PaddingTop(15).Text(text =>
                    {
                        text.Span($"La {data.CompanyName}, identificada con NIT {data.CompanyNit}, certifica que ");
                        text.Span(data.EmployeeName).Bold();
                        text.Span($", identificado(a) con cedula de ciudadania No. {data.EmployeeNit}, ");
                        text.Span(isActive ? "se encuentra vinculado(a)" : "estuvo vinculado(a)");
                        text.Span($" a nuestra entidad {tenure}, desempenando el cargo de ");
                        text.Span(data.Position).Bold();
                        text.Span($" en el area de {data.Department}, mediante contrato {data.ContractType}.");
                    });

                    col.Item().PaddingTop(10).Text(text =>
                    {
                        text.Span($"Su asignacion salarial {(isActive ? "actual es" : "al momento del retiro era")} de ");
                        text.Span($"${data.CurrentSalary:N2}").Bold();
                        text.Span(" mensuales.");
                    });

                    col.Item().PaddingTop(10).Text(
                        "La presente certificacion se expide a solicitud del interesado(a) para los fines que estime convenientes.");

                    col.Item().PaddingTop(10).Text("Cordialmente,");

                    // Signature
                    col.Item().PaddingTop(40).Column(c =>
                    {
                        c.Item().ExtendHorizontal().LineHorizontal(0.5f);
                        c.Item().Text(data.SignatoryName).Bold();
                        c.Item().Text(data.SignatoryTitle).FontSize(9);
                        c.Item().Text(data.CompanyName).FontSize(9);
                    });
                });

                page.Footer().AlignCenter().Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7);
            });
        });

        return document.GeneratePdf();
    }

    private static string MonthName(int month) => month switch
    {
        1 => "enero", 2 => "febrero", 3 => "marzo", 4 => "abril",
        5 => "mayo", 6 => "junio", 7 => "julio", 8 => "agosto",
        9 => "septiembre", 10 => "octubre", 11 => "noviembre", 12 => "diciembre",
        _ => ""
    };
}
