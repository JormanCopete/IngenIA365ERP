using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using QRCoder;

namespace IngenIA365ERP.API.IntegrationTests.Reports;

/// <summary>
/// Feature 013, US13, T0767 (adaptadores-y-archivos.md §8.10; molde <see cref="RepresentacionGraficaReportTests"/>), sin contenedores: las tres clases de
/// K1 producen un PDF válido con el membrete (nombre y NIT de la cooperativa), el firmante, el número, la fecha, el código, la copia de la
/// identificación del titular y un QR cuyo contenido es <c>{urlDeLaAplicacion}/cartera/verificar?codigo=XXXX-XXXX-XXXX-XXXX</c>; con ambiente de
/// pruebas lleva «AMBIENTE DE PRUEBAS – SIN VALIDEZ»; el paz y salvo sujeto a firmeza, «La cancelación está sujeta a firmeza hasta el {fecha}»; el
/// general, su alcance, si incluyó codeudas y las ventas fuera de Cartera; el mismo modelo da los mismos bytes. El PDF lleva el texto como glifos:
/// lo que dice se comprueba sobre <see cref="DocumentosDeCarteraReport.TextosImpresos"/>, la misma fuente de la que pinta la plantilla.
/// </summary>
public class DocumentosDeCarteraReportTests
{
    static DocumentosDeCarteraReportTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private const string Codigo = "7K2M-Q9XD-4HTP-B1RW";
    private const string Url = "https://erp.coop.test/cartera/verificar?codigo=" + Codigo;
    private static readonly DateOnly Hoy = new(2026, 12, 7);

    private static EncabezadoDeDocumentoDeCartera Encabezado(IssuedDocumentKind clase, string numero, bool pruebas, params string[] leyendas)
    {
        var todas = new List<string>();
        if (pruebas) todas.Add(EncabezadoDeDocumentoDeCartera.MarcaDePruebas);
        todas.AddRange(leyendas);
        return new EncabezadoDeDocumentoDeCartera(clase, ModelosDeDocumentoDeCartera.Titulo(clase), numero,
            new DateTimeOffset(2026, 12, 7, 10, 14, 22, TimeSpan.FromHours(-5)), "COOPERATIVA DE PRUEBA", "900123456-7", "ANA PÉREZ GÓMEZ", "C", "16000110",
            "MARTA LÓPEZ", "Gerente", Codigo, ModelosDeDocumentoDeCartera.UrlDeVerificacion("https://erp.coop.test/", Codigo), pruebas, todas);
    }

    private static DocumentoDeCarteraParaImprimir Plan(bool pruebas = true) => new(
        Encabezado(IssuedDocumentKind.PaymentPlan, "PP-000001", pruebas),
        new CuerpoDePlanDePagos("CR-000123", "Consumo libre inversión", 1, new DateOnly(2026, 11, 2), 3_000_000m, 0.24m, 0.2178m, "Monthly", 3,
        [
            new CuotaDelPlanImpresa(1, new DateOnly(2026, 12, 2), 970_000m, 54_000m, 3_000m, 0m, 1_027_000m, 2_030_000m),
            new CuotaDelPlanImpresa(2, new DateOnly(2027, 1, 2), 990_000m, 36_000m, 2_000m, 0m, 1_028_000m, 1_040_000m),
            new CuotaDelPlanImpresa(3, new DateOnly(2027, 2, 2), 1_040_000m, 18_000m, 1_000m, 0m, 1_059_000m, 0m),
        ]));

    private static DocumentoDeCarteraParaImprimir PazYSalvo(DateOnly firmeza) => new(
        Encabezado(IssuedDocumentKind.LoanClearance, "PS-000001", true, string.Format(System.Globalization.CultureInfo.GetCultureInfo("es-CO"),
            ModelosDeDocumentoDeCartera.LeyendaDeFirmeza, firmeza.ToString("dd/MM/yyyy"))),
        new CuerpoDePazYSalvo(false, Hoy, [new CreditoCertificado("CR-000123", "Consumo libre inversión", "Deudor", 0m)], firmeza, null, []));

    private static DocumentoDeCarteraParaImprimir General() => new(
        Encabezado(IssuedDocumentKind.GeneralClearance, "PSG-000001", true, CuerpoDePazYSalvo.LeyendaDeAlcance, ModelosDeDocumentoDeCartera.LeyendaConCodeudas,
            string.Format(ModelosDeDocumentoDeCartera.LeyendaDeVentasFueraDeCartera, "de las facturas FV-0099")),
        new CuerpoDePazYSalvo(true, Hoy,
            [new CreditoCertificado("CR-000123", "Consumo", "Deudor", 0m), new CreditoCertificado("CR-000200", "Vivienda", "Codeudor", 0m)], null, true,
            [new VentaFueraDeCartera("FV-0099", new DateOnly(2026, 6, 1), 450_000m)]));

    private static DocumentoDeCarteraParaImprimir Certificado() => new(
        Encabezado(IssuedDocumentKind.BalanceCertificate, "CS-000001", true),
        new CuerpoDeCertificadoDeSaldo("CR-000123", "Consumo libre inversión", Hoy,
            [new ConceptoCertificado("Principal", 2_030_000m), new ConceptoCertificado("CurrentInterest", 12_345.67m), new ConceptoCertificado("Insurance", 3_000m)],
            2_045_345.67m, 5, false, null, new string('a', 64), "R-2026.1"));

    private static byte[] Pintar(DocumentoDeCarteraParaImprimir d) => new DocumentoDeCarteraPdfRenderer().Render(d);

    public static TheoryData<string> Clases => new() { "plan", "paz", "general", "certificado" };

    private static DocumentoDeCarteraParaImprimir Modelo(string clase) => clase switch
    {
        "plan" => Plan(),
        "paz" => PazYSalvo(new DateOnly(2026, 12, 10)),
        "general" => General(),
        _ => Certificado(),
    };

    [Theory]
    [MemberData(nameof(Clases))]
    public void Cada_clase_pinta_un_PDF_con_el_encabezado_comun_y_el_QR_de_verificacion(string clase)
    {
        var modelo = Modelo(clase);
        var pdf = Pintar(modelo);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        pdf.Length.Should().BeGreaterThan(1000);
        Encoding.Latin1.GetString(pdf).Should().Contain("612", "es carta: 612 pt de ancho");

        var textos = DocumentosDeCarteraReport.TextosImpresos(modelo);
        textos.Should().Contain("COOPERATIVA DE PRUEBA").And.Contain("NIT 900123456-7", "el membrete con el NIT de la cooperativa");
        textos.Should().Contain(t => t.Contains(modelo.Encabezado.Numero) && t.Contains("07/12/2026 10:14"), "número y fecha en hora de Colombia");
        textos.Should().Contain("Titular: ANA PÉREZ GÓMEZ · C 16000110", "la copia de la identificación del titular");
        textos.Should().Contain("MARTA LÓPEZ").And.Contain("Gerente", "el firmante y su cargo");
        textos.Should().Contain($"Código de verificación: {Codigo}");
        DocumentosDeCarteraReport.ContenidoDelQr(modelo).Should().Be(Url);
    }

    [Fact]
    public void En_pruebas_lleva_la_marca_sin_validez_y_en_produccion_no()
    {
        DocumentosDeCarteraReport.TextosImpresos(Plan(pruebas: true)).Should().Contain("AMBIENTE DE PRUEBAS – SIN VALIDEZ");
        DocumentosDeCarteraReport.TextosImpresos(Plan(pruebas: false)).Should().NotContain("AMBIENTE DE PRUEBAS – SIN VALIDEZ");
        Pintar(Plan(pruebas: true)).Should().NotEqual(Pintar(Plan(pruebas: false)), "la marca de agua y la leyenda cambian el PDF");
    }

    [Fact]
    public void El_plan_lleva_las_cuotas_y_las_tasas_efectiva_y_nominal()
    {
        var textos = DocumentosDeCarteraReport.TextosImpresos(Plan());

        textos.Should().Contain("Crédito CR-000123 · Consumo libre inversión");
        textos.Should().Contain(t => t.Contains("3 cuotas mensuales"));
        textos.Should().Contain(t => t.Contains("Tasa efectiva anual: 24 % EA") && t.Contains("nominal anual: 21,78 %"));
        textos.Should().Contain(t => t.StartsWith("Total de las cuotas: $ 3.114.000,00"));
    }

    [Fact]
    public void El_paz_y_salvo_sujeto_a_firmeza_lo_dice_con_la_fecha()
    {
        DocumentosDeCarteraReport.TextosImpresos(PazYSalvo(new DateOnly(2026, 12, 10)))
            .Should().Contain("La cancelación está sujeta a firmeza hasta el 10/12/2026.");
    }

    [Fact]
    public void El_general_dice_su_alcance_las_codeudas_y_las_ventas_fuera_de_Cartera()
    {
        var textos = DocumentosDeCarteraReport.TextosImpresos(General());

        textos.Should().Contain("Certifica sólo las obligaciones de crédito de la cooperativa.");
        textos.Should().Contain("Incluye las obligaciones en las que la persona es codeudora.");
        textos.Should().Contain(t => t.Contains("FV-0099") && t.Contains("no hacen parte de Cartera"));
        textos.Should().Contain(t => t.StartsWith("Factura FV-0099 del 01/06/2026"));
        textos.Should().Contain(t => t.StartsWith("CR-000200") && t.Contains("Codeudor"));
    }

    [Fact]
    public void El_certificado_de_saldo_va_por_concepto_con_lo_que_cuesta_cancelar()
    {
        var textos = DocumentosDeCarteraReport.TextosImpresos(Certificado());

        textos.Should().Contain(t => t.StartsWith("Capital $ 2.030.000,00"));
        textos.Should().Contain(t => t.StartsWith("Interés corriente $ 12.345,67"));
        textos.Should().Contain(t => t.StartsWith("Para cancelar: $ 2.045.345,67") && t.Contains("días de mora: 5"));
        textos.Should().Contain(t => t.Contains("reglas R-2026.1"));
    }

    [Theory]
    [MemberData(nameof(Clases))]
    public void El_mismo_modelo_da_los_mismos_bytes(string clase)
    {
        Pintar(Modelo(clase)).Should().Equal(Pintar(Modelo(clase)), "la fecha de los metadatos es la de emisión, no la del reloj");
    }

    [Fact]
    public void El_logotipo_y_la_imagen_de_la_firma_van_solo_si_existen()
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode("logotipo", QRCodeGenerator.ECCLevel.L);
        var png = new PngByteQRCode(datos).GetGraphic(4);
        var sin = Plan();
        var con = sin with { Encabezado = sin.Encabezado with { Logotipo = png, ImagenDeFirma = png } };

        Pintar(con).Length.Should().BeGreaterThan(Pintar(sin).Length);
    }
}
