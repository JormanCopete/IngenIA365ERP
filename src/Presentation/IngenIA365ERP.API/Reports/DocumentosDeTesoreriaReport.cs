using System.Globalization;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Los documentos de Tesorería (feature 015, US19, T0229; archivos-y-plantillas.md §7): el cheque girado, impreso sobre el formato de la chequera —fecha, beneficiario,
/// valor en números y en letras, cruce y «páguese al primer beneficiario»— con la orden y el concepto en el talón. (nuevo)
/// </summary>
public static class DocumentosDeTesoreriaReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Cheque(ChequeModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(new PageSize(17.5f, 8f, Unit.Centimetre));
            page.Margin(6, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(9));
            page.Content().Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text($"{m.BankName} · cuenta {m.AccountNumber}").Bold();
                    r.ConstantItem(140).AlignRight().Text($"Cheque No. {m.CheckNumber.ToString(Co)}").Bold();
                });
                col.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text($"Fecha: {m.IssuedOn.ToString("yyyy-MM-dd", Co)}");
                    r.ConstantItem(140).AlignRight().Border(0.5f).Padding(2).Text($"$ {m.Amount.ToString("N2", Co)}").Bold();
                });
                col.Item().PaddingTop(6).Text($"Páguese a la orden de: {m.Beneficiary}");
                col.Item().PaddingTop(2).Text($"La suma de: {m.AmountInWords}");
                if (m.Crossed || m.FirstBeneficiaryOnly)
                    col.Item().PaddingTop(4).Text(string.Join(" · ", new[]
                    {
                        m.Crossed ? "CRUZADO" : null, m.FirstBeneficiaryOnly ? "PÁGUESE ÚNICAMENTE AL PRIMER BENEFICIARIO" : null,
                    }.Where(x => x is not null))).Bold();
                col.Item().PaddingTop(10).Row(r =>
                {
                    r.RelativeItem().Text($"{m.Company} · orden {m.OrderNumber} · {m.Concept}").FontSize(7);
                    r.ConstantItem(160).Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().AlignCenter().Text("Firma autorizada").FontSize(7);
                    });
                });
            });
        })).GeneratePdf();
    }
}

/// <summary><see cref="IDocumentosDeTesoreriaEnPdf"/> con QuestPDF. (nuevo)</summary>
public sealed class DocumentosDeTesoreriaPdfRenderer : IDocumentosDeTesoreriaEnPdf
{
    public byte[] Cheque(ChequeModel modelo) => DocumentosDeTesoreriaReport.Cheque(modelo);
}
