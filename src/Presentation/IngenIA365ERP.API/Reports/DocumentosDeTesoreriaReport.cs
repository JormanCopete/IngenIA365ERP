using System.Globalization;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Application.Treasury.Reconciliation;
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

    /// <summary>
    /// El informe de la conciliación cerrada (feature 015, US25, T0279; FR-156, FR-217): saldo del extracto, las partidas conciliatorias por clase con su antigüedad y su
    /// efecto, el saldo en libros, la diferencia (cero al cerrar) y las firmas de quien concilia y quien revisa.
    /// </summary>
    public static byte[] Conciliacion(ConciliacionModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(15, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(9));
            page.Header().Column(c =>
            {
                c.Item().Text(m.Company).Bold().FontSize(12);
                c.Item().Text($"Conciliación bancaria · {m.BankName} · cuenta {m.AccountCode} ({m.AccountNumber})").Bold();
                c.Item().Text($"Período {m.Year.ToString(Co)}-{m.Month.ToString("00", Co)} · versión {m.Version.ToString(Co)} · cerrada el {m.ClosedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Co)}");
            });
            page.Content().PaddingTop(8).Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("Saldo según el extracto");
                    r.ConstantItem(120).AlignRight().Text(m.StatementBalance.ToString("N2", Co));
                });
                col.Item().PaddingTop(6).Text("Partidas conciliatorias").Bold();
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                    });
                    t.Header(h =>
                    {
                        h.Cell().Text("Clase").Bold();
                        h.Cell().Text("Desde").Bold();
                        h.Cell().AlignRight().Text("Días").Bold();
                        h.Cell().AlignRight().Text("Efecto").Bold();
                        h.Cell().Text("Nota").Bold();
                    });
                    foreach (var p in m.Items.OrderBy(i => i.Kind, StringComparer.Ordinal).ThenBy(i => i.Since))
                    {
                        t.Cell().Text(p.Kind);
                        t.Cell().Text(p.Since.ToString("yyyy-MM-dd", Co));
                        t.Cell().AlignRight().Text(p.AgeDays.ToString(Co));
                        t.Cell().AlignRight().Text(p.SignedAmount.ToString("N2", Co));
                        t.Cell().Text(p.Note ?? string.Empty);
                    }
                });
                col.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Text("Saldo en libros");
                    r.ConstantItem(120).AlignRight().Text(m.LedgerBalance.ToString("N2", Co));
                });
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("Diferencia").Bold();
                    r.ConstantItem(120).AlignRight().Text(m.Difference.ToString("N2", Co)).Bold();
                });
                col.Item().PaddingTop(4).Text($"Líneas del extracto cruzadas: {m.MatchedLines.ToString(Co)}").FontSize(8);
                col.Item().PaddingTop(30).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text($"Concilió: {m.Conciliator}").FontSize(8);
                    });
                    r.ConstantItem(30);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().Text("Revisó").FontSize(8);
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

    public byte[] Conciliacion(ConciliacionModel modelo) => DocumentosDeTesoreriaReport.Conciliacion(modelo);
}
