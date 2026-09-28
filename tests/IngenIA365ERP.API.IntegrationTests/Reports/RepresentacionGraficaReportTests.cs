using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.API.IntegrationTests.Reports;

/// <summary>
/// La representación gráfica de los documentos electrónicos (feature 012, I4, T750; contracts/dian.md §13.1), sin contenedores: una sola
/// plantilla en carta y en tirilla de 80 mm, desde el canónico, con el QR del canal cuando lo hay y sin él en el papel de contingencia 03.
/// </summary>
public class RepresentacionGraficaReportTests
{
    static RepresentacionGraficaReportTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static DocumentoElectronicoCanonico Canonico(ElectronicDocumentKind tipo) => new()
    {
        Kind = tipo,
        DianDocumentTypeCode = "01",
        Environment = DianEnvironment.Testing,
        Number = new NumeroCanonico("SETP", 990000123, "SETP990000123"),
        Resolution = new ResolucionCanonica("18760000001", new DateOnly(2025, 12, 20), 990000000, 995000000, new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31)),
        IssuedAt = new DateTimeOffset(2026, 12, 5, 10, 14, 22, TimeSpan.FromHours(-5)),
        Currency = "COP",
        PaymentForm = "Cash",
        Issuer = new ParteCanonica { TaxId = "900123456", CheckDigit = "7", Name = "COOPERATIVA DE PRUEBA", Email = "facturas@coop.co" },
        Counterparty = new ParteCanonica { TaxId = "16000110", Name = "ANA PÉREZ", Address = new DireccionCanonica("CALLE 5", "76001", "CO") },
        Lines =
        [
            new LineaCanonica
            {
                LineNumber = 1, ProductCode = "ARZ-001", Description = "ARROZ 500 G", Quantity = 2m, UnitCode = "94", UnitPrice = 2100m,
                LineExtension = 3990m, Taxes = [new ImpuestoCanonico("01", 0.19m, null, null, 3990m, 758.10m)],
            },
        ],
        Payments = [new PagoCanonico("10", 4748.10m, null)],
        Totals = new TotalesCanonicos { LineExtension = 3990m, Taxes = 758.10m, Payable = 4748.10m, AmountDue = 4748.10m },
    };

    private static byte[] Pintar(ElectronicDocumentKind tipo, string? qr, ElectronicDocumentStatus estado = ElectronicDocumentStatus.Validated) =>
        new RepresentacionGraficaRenderer().Renderizar(new SolicitudDeRepresentacion(
            Canonico(tipo), estado, null, qr is null ? null : "cufe-abc", qr,
            LeyendasDeRepresentacion.Para(tipo, estado, null, DianEnvironment.Testing, qr is not null),
            LeyendasDeRepresentacion.FormatoDe(tipo)));

    [Theory]
    [InlineData(ElectronicDocumentKind.Invoice)]
    [InlineData(ElectronicDocumentKind.PosEquivalent)]
    [InlineData(ElectronicDocumentKind.SupportDocument)]
    public void Pinta_un_PDF_en_el_formato_del_tipo(ElectronicDocumentKind tipo)
    {
        var pdf = Pintar(tipo, "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=abc");

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        pdf.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void El_QR_sale_solo_cuando_el_canal_lo_devolvio()
    {
        var conQr = Pintar(ElectronicDocumentKind.Invoice, "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=abc");
        var sinQr = Pintar(ElectronicDocumentKind.Invoice, null, ElectronicDocumentStatus.IssuerContingency);

        conQr.Length.Should().BeGreaterThan(sinQr.Length);
    }

    [Fact]
    public void La_tirilla_es_mas_angosta_que_la_carta()
    {
        // 80 mm son 226,8 pt; la carta mide 612 pt. El PDF declara el ancho en su MediaBox.
        var tirilla = Encoding.Latin1.GetString(Pintar(ElectronicDocumentKind.PosEquivalent, null));
        var carta = Encoding.Latin1.GetString(Pintar(ElectronicDocumentKind.Invoice, null));

        carta.Should().Contain("612");
        tirilla.Should().MatchRegex(@"MediaBox\s*\[\s*0\s+0\s+226\.");
    }
}
