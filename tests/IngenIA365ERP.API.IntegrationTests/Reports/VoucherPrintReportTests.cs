using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Accounting.Documents;

namespace IngenIA365ERP.API.IntegrationTests.Reports;

/// <summary>
/// 2026-11-16 (pedido del dueño), sin contenedores: el comprobante contable sale en carta vertical (612 × 792 puntos) con el diseño nuevo,
/// contabilizado o en borrador, y aguanta muchas líneas en varias páginas. Con <c>COMPROBANTE_PDF_DIR</c> deja los PDF en esa carpeta para
/// mirarlos a mano.
/// </summary>
public class VoucherPrintReportTests
{
    static VoucherPrintReportTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static readonly EmpresaParaImpresion Empresa = new("COOPERATIVA DE PRUEBA", "900123456-7");

    private static ComprobanteDto Comprobante(string estado, int lineas)
    {
        var detalle = Enumerable.Range(1, lineas).Select(i => new LineaDeComprobanteDto(
            i, Guid.NewGuid(), i % 2 == 1 ? "51101001" : "23359501", i % 2 == 1 ? "HONORARIOS ASESORÍA CONTABLE" : "HONORARIOS POR PAGAR",
            Guid.NewGuid(), "PRINCIPAL", i % 2 == 1 ? Guid.NewGuid() : null, i % 2 == 1 ? "ADMINISTRACIÓN" : null,
            Guid.NewGuid(), "ANA MARÍA PÉREZ GÓMEZ", "1000000001", i % 2 == 0 ? "FC" : null, i % 2 == 0 ? "FC-0042" : null,
            i % 2 == 1 ? 1_250_000m : 0m, i % 2 == 0 ? 1_250_000m : 0m, "Honorarios de octubre", i % 2 == 1 ? 1_250_000m : null)).ToList();
        var total = detalle.Sum(l => l.Debit);
        return new ComprobanteDto(Guid.NewGuid(), "CG", "Comprobante general", estado == "Draft" ? null : 42, new DateOnly(2026, 11, 16),
            "Causación de honorarios de octubre", estado, "Regular", total, detalle.Sum(l => l.Credit), "Contadora", new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc),
            estado == "Draft" ? null : "Revisora", estado == "Draft" ? null : new DateTime(2026, 11, 16, 16, 0, 0, DateTimeKind.Utc),
            new OrigenDeComprobanteDto("CNT", "Contabilidad", null, null, null), new ReversionDto(null, null, null, null, null), detalle, [], []);
    }

    [Theory]
    [InlineData("Posted", 2)]
    [InlineData("Draft", 6)]
    [InlineData("Posted", 120)]
    public void Sale_en_carta_vertical(string estado, int lineas)
    {
        var pdf = VoucherPrintReport.Generate(Comprobante(estado, lineas), Empresa);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        // MediaBox de carta vertical: 612 de ancho por 792 de alto (antes 792 × 612, apaisada).
        Encoding.Latin1.GetString(pdf).Should().Contain("/MediaBox [0 0 612 792]");

        if (Environment.GetEnvironmentVariable("COMPROBANTE_PDF_DIR") is { Length: > 0 } carpeta)
            File.WriteAllBytes(Path.Combine(carpeta, $"comprobante-{estado}-{lineas}.pdf"), pdf);
    }
}
