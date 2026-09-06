using System.Globalization;
using IngenIA365ERP.Application.Payroll.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Comprobante de pago de nómina (feature 005, FR-024) sobre <see cref="PayslipModel"/>.
/// Devengos y deducciones con la explicación en una línea, salario y días en el
/// encabezado, banco y cuenta, y el estado de pago. El PDF no calcula nada: pinta lo que
/// la liquidación aprobada dejó guardado.
/// </summary>
public static class PayslipReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(PayslipModel data) =>
        Document.Create(c => Pagina(c, data)).GeneratePdf();

    public static byte[] GenerateMany(IReadOnlyList<PayslipModel> items) =>
        Document.Create(c =>
        {
            foreach (var item in items) Pagina(c, item);
        }).GeneratePdf();

    private static void Pagina(IDocumentContainer container, PayslipModel data)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Column(col =>
            {
                col.Item().Text(data.CooperativeName).Bold().FontSize(14);
                if (!string.IsNullOrWhiteSpace(data.CooperativeTaxId))
                    col.Item().Text($"NIT: {data.CooperativeTaxId}").FontSize(8);
                col.Item().PaddingTop(5).AlignCenter().Text("COMPROBANTE DE PAGO DE NÓMINA").FontSize(12).SemiBold();
                col.Item().AlignCenter().Text($"{data.PlanName} · {data.PeriodLabel} ({data.PeriodStart:dd/MM/yyyy} – {data.PeriodEnd:dd/MM/yyyy}) · liquidación v{data.RunVersion}").FontSize(9);
                col.Item().PaddingBottom(5).LineHorizontal(1);
            });

            page.Content().PaddingVertical(5).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Empleado: {data.EmployeeName}").SemiBold();
                        c.Item().Text($"Documento: {data.EmployeeDocument}");
                        if (!string.IsNullOrWhiteSpace(data.EmployeePosition)) c.Item().Text($"Cargo: {data.EmployeePosition}");
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Salario mensual: {Pesos(data.MonthlySalary)}");
                        c.Item().Text($"Días pagados: {data.DaysWorked}");
                        if (data.ApprovedAt is { } ap) c.Item().Text($"Aprobada: {ap:dd/MM/yyyy}");
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Banco: {data.BankName ?? "—"}");
                        c.Item().Text($"Cuenta: {data.BankAccount ?? "—"}");
                        if (!string.IsNullOrWhiteSpace(data.PaymentStatus)) c.Item().Text(data.PaymentStatus);
                    });
                });

                col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                Bloque(col, "DEVENGOS", data.Earnings, data.TotalEarnings);
                col.Item().PaddingVertical(6);
                Bloque(col, "DEDUCCIONES", data.Deductions, data.TotalDeductions);

                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text("NETO A PAGAR").Bold().FontSize(12);
                    row.ConstantItem(150).AlignRight().Text(Pesos(data.NetPay)).Bold().FontSize(12);
                });

                col.Item().PaddingTop(30).Row(row =>
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

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Generado el {data.GeneratedAt:dd/MM/yyyy HH:mm} UTC").FontSize(7);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.DefaultTextStyle(s => s.FontSize(7));
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });
    }

    private static void Bloque(ColumnDescriptor col, string titulo, IReadOnlyList<PayslipLineModel> lineas, decimal total)
    {
        col.Item().Text(titulo).Bold().FontSize(10);
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(80);
                cols.RelativeColumn(2);
                cols.RelativeColumn(3);
                cols.ConstantColumn(55);
                cols.ConstantColumn(95);
            });
            table.Header(h =>
            {
                h.Cell().BorderBottom(1).Padding(2).Text("Código").Bold();
                h.Cell().BorderBottom(1).Padding(2).Text("Concepto").Bold();
                h.Cell().BorderBottom(1).Padding(2).Text("Cómo se calculó").Bold();
                h.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Cant.").Bold();
                h.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Valor").Bold();
            });
            if (lineas.Count == 0)
            {
                table.Cell().ColumnSpan(5).Padding(2).Text("Sin líneas.").FontSize(8).Italic();
            }
            foreach (var l in lineas)
            {
                table.Cell().Padding(2).Text(l.Code).FontSize(8);
                table.Cell().Padding(2).Text(l.Name).FontSize(8);
                table.Cell().Padding(2).Text(l.Summary).FontSize(7);
                table.Cell().Padding(2).AlignRight().Text(l.Quantity is { } q ? q.ToString("0.##", Co) : string.Empty).FontSize(8);
                table.Cell().Padding(2).AlignRight().Text(Pesos(l.Amount)).FontSize(8);
            }
        });
        col.Item().BorderTop(1).PaddingTop(3).Row(row =>
        {
            row.RelativeItem().Text($"TOTAL {titulo}").Bold();
            row.ConstantItem(95).AlignRight().Text(Pesos(total)).Bold();
        });
    }

    private static string Pesos(decimal v) => v.ToString("N0", Co);
}

/// <summary><see cref="IPayslipPdfRenderer"/> con QuestPDF (D-12). Registrado en la API, que es la única capa que conoce la librería.</summary>
public sealed class PayslipPdfRenderer : IPayslipPdfRenderer
{
    public byte[] Render(PayslipModel payslip) => PayslipReport.Generate(payslip);
    public byte[] RenderMany(IReadOnlyList<PayslipModel> payslips) => PayslipReport.GenerateMany(payslips);
}
