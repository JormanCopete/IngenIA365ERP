using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Documents;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US13 (T194, T197; contracts/documentos.md §1.2, §2.3, §2.4, §4; FR-101, FR-104, FR-105, SC-016): lo puro de los documentos al titular —el
/// reparto por la proporción de cada titular, el certificado tributario que cuadra al centavo con dos cotitulares al 60/40, el certificado de retención
/// agrupado por clase y tarifa, qué anula el reverso de una operación de cuenta y el estado de los títulos (reemplazado, liquidado). (nuevo)
/// </summary>
public sealed class DocumentosAlTitularTests
{
    private const int Ana = 1;
    private const int Beto = 2;
    private const int Carla = 3;

    // ------------------------------------------------------------------------------------------------------- proporciones --

    [Fact]
    public void Con_la_proporcion_de_los_cotitulares_el_titular_lleva_el_resto()
    {
        var p = ProporcionesDeTitularidad.Calcular(Ana, [(Beto, 0.40m)]);
        p.Should().Equal((Ana, 0.60m), (Beto, 0.40m));
    }

    [Fact]
    public void Sin_proporcion_van_por_partes_iguales_y_el_titular_absorbe_el_redondeo()
    {
        var p = ProporcionesDeTitularidad.Calcular(Ana, [(Beto, null), (Carla, 0.5m)]);
        p.Select(x => x.Proporcion).Should().Equal(0.333334m, 0.333333m, 0.333333m);
        p.Sum(x => x.Proporcion).Should().Be(1m);
    }

    [Fact]
    public void El_reparto_al_centavo_suma_exactamente_el_total()
    {
        var p = ProporcionesDeTitularidad.Calcular(Ana, [(Beto, null), (Carla, null)]);
        var partes = ProporcionesDeTitularidad.Repartir(100.00m, p);
        partes[Beto].Should().Be(33.33m);
        partes[Carla].Should().Be(33.33m);
        partes[Ana].Should().Be(33.34m, "el titular absorbe el residuo");
        partes.Values.Sum().Should().Be(100.00m);
    }

    // ------------------------------------------------------------------------------------------------ certificado tributario --

    private static CuentaParaElCertificadoTributario Cuenta6040(decimal saldo = 1_234_567.89m, decimal gmf = 4_938.27m) =>
        new("AHVISTA-000001", "Ahorro a la vista", false, ProporcionesDeTitularidad.Calcular(Ana, [(Beto, 0.40m)]), saldo, gmf,
        [
            new AcumuladoTributario(Ana, 60_000.01m, 33_258.01m, 26_742.00m, 1_871.94m),
            new AcumuladoTributario(Beto, 40_000.00m, 22_172.00m, 17_828.00m, 1_247.96m),
        ]);

    [Fact]
    public void Con_dos_cotitulares_al_60_40_los_dos_certificados_suman_exactamente_lo_de_la_cuenta_SC016()
    {
        var cuenta = Cuenta6040();
        var deAna = CertificadoTributarioDeCaptaciones.Armar(2026, Ana, [cuenta], incluyeAportes: false);
        var deBeto = CertificadoTributarioDeCaptaciones.Armar(2026, Beto, [cuenta], incluyeAportes: false);

        (deAna.TotalSaldo + deBeto.TotalSaldo).Should().Be(1_234_567.89m);
        (deAna.TotalGmf + deBeto.TotalGmf).Should().Be(4_938.27m);
        (deAna.TotalRendimientos + deBeto.TotalRendimientos).Should().Be(100_000.01m);
        (deAna.TotalRetencion + deBeto.TotalRetencion).Should().Be(3_119.90m);
        deBeto.TotalSaldo.Should().Be(493_827.16m);
        deBeto.Captaciones.Single().Proporcion.Should().Be(0.40m);
        deAna.TotalComponenteInflacionario.Should().Be(33_258.01m);
    }

    [Fact]
    public void Los_aportes_sólo_entran_si_la_cooperativa_los_incluye_y_el_GMF_migrado_suma_a_la_persona()
    {
        var aportes = new CuentaParaElCertificadoTributario("APORTES-000001", "Aportes", true, [(Ana, 1m)], 900_000m, 0m, [], RevalorizacionDelAño: 18_000m);
        var conMigrado = Cuenta6040() with { GmfMigradoDeLaPersona = 120m };

        var sin = CertificadoTributarioDeCaptaciones.Armar(2026, Ana, [conMigrado, aportes], incluyeAportes: false);
        var con = CertificadoTributarioDeCaptaciones.Armar(2026, Ana, [conMigrado, aportes], incluyeAportes: true);

        sin.Aportes.Should().BeEmpty();
        con.TotalAportes.Should().Be(900_000m);
        con.TotalRevalorizacion.Should().Be(18_000m);
        sin.TotalGmf.Should().Be(2_962.96m + 120m);
    }

    [Fact]
    public void Una_persona_sin_productos_en_el_año_no_tiene_nada_que_certificar()
    {
        CertificadoTributarioDeCaptaciones.Armar(2026, Carla, [Cuenta6040()], incluyeAportes: true).Vacio.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------- certificado de retención --

    [Fact]
    public void El_certificado_de_retencion_agrupa_por_clase_y_tarifa_y_neta_los_reversos()
    {
        var renglones = CertificadoDeRetencionDeCaptaciones.Agrupar(
        [
            new RetencionDelPeriodo("Ahorro a la vista", 0.07m, 10_000m, 5_543m, 4_457m, 312m),
            new RetencionDelPeriodo("Ahorro a la vista", 0.07m, 12_000m, 6_652m, 5_348m, 374m),
            new RetencionDelPeriodo("CDAT", 0.04m, 488_088m, 0m, 488_088m, 19_524m),
            new RetencionDelPeriodo("CDAT", 0.04m, -488_088m, 0m, -488_088m, -19_524m),
        ]);

        renglones.Should().ContainSingle("el CDAT reversado no deja renglón");
        renglones[0].Retencion.Should().Be(686m);
        renglones[0].BaseDeRetencion.Should().Be(9_805m);
    }

    // ------------------------------------------------------------------------------------------------- anulación por reverso --

    private static readonly DateOnly Dic15 = new(2026, 12, 15);

    [Fact]
    public void Reversar_una_retencion_de_diciembre_anula_el_certificado_tributario_del_año_y_el_de_retencion_del_periodo()
    {
        var retencion = new OperacionDeCuentaReversada(10, Dic15, PagoCuotas: false, Tributaria: true);
        VigenciaDeDocumentosDeCuentas.Anula(new DocumentoDeCuentaQueCubre(IssuedDocumentKind.DepositTaxCertificate, new(2026, 12, 31), null, null, 2026, null),
            retencion).Should().BeTrue();
        VigenciaDeDocumentosDeCuentas.Anula(new DocumentoDeCuentaQueCubre(IssuedDocumentKind.DepositTaxCertificate, new(2025, 12, 31), null, null, 2025, null),
            retencion).Should().BeFalse("el año anterior no cambia");
        VigenciaDeDocumentosDeCuentas.Anula(new DocumentoDeCuentaQueCubre(IssuedDocumentKind.WithholdingCertificate, null, new(2026, 12, 1), new(2026, 12, 31),
            null, null), retencion).Should().BeTrue();
        VigenciaDeDocumentosDeCuentas.Anula(new DocumentoDeCuentaQueCubre(IssuedDocumentKind.WithholdingCertificate, null, new(2026, 11, 1), new(2026, 11, 30),
            null, null), retencion).Should().BeFalse("la retención es de otro período");
    }

    [Fact]
    public void Un_reverso_que_no_es_tributario_no_anula_el_certificado_de_retencion_pero_si_el_extracto_y_el_certificado_de_saldo()
    {
        var consignacion = new OperacionDeCuentaReversada(11, Dic15, PagoCuotas: false, Tributaria: false);
        VigenciaDeDocumentosDeCuentas.Anula(new DocumentoDeCuentaQueCubre(IssuedDocumentKind.WithholdingCertificate, null, new(2026, 12, 1), new(2026, 12, 31),
            null, null), consignacion).Should().BeFalse();
        VigenciaDeDocumentosDeCuentas.Anula(new DocumentoDeCuentaQueCubre(IssuedDocumentKind.AccountStatement, null, new(2026, 12, 1), new(2026, 12, 31), null, null),
            consignacion).Should().BeTrue();
        VigenciaDeDocumentosDeCuentas.Anula(new DocumentoDeCuentaQueCubre(IssuedDocumentKind.AccountBalanceCertificate, new(2026, 12, 10), null, null, null, null),
            consignacion).Should().BeFalse("el certificado es a una fecha anterior a la fecha valor");
        VigenciaDeDocumentosDeCuentas.Anula(new DocumentoDeCuentaQueCubre(IssuedDocumentKind.GarnishmentResponse, Dic15, null, null, null, null), consignacion)
            .Should().BeFalse("lo que se respondió al juzgado no se anula");
    }

    [Fact]
    public void El_titulo_solo_lo_anula_el_reverso_de_la_operacion_que_lo_sello()
    {
        var titulo = new DocumentoDeCuentaQueCubre(IssuedDocumentKind.TermDepositTitle, Dic15, null, null, null, OperacionQueLoSello: 77);
        VigenciaDeDocumentosDeCuentas.Anula(titulo, new OperacionDeCuentaReversada(77, Dic15, false, false)).Should().BeTrue();
        VigenciaDeDocumentosDeCuentas.Anula(titulo, new OperacionDeCuentaReversada(78, Dic15, false, true)).Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------------------------- estados --

    [Fact]
    public void El_titulo_reemplazado_o_liquidado_lo_dice_su_estado_y_la_anulacion_manda()
    {
        var hoy = new DateOnly(2026, 12, 1);
        EstadoDeDocumentoEmitido.Calcular(EventosDeEstado.De([IssuedDocumentEventKind.Issued, IssuedDocumentEventKind.Superseded]), null, hoy)
            .Should().Be(IssuedDocumentStatus.Superseded);
        EstadoDeDocumentoEmitido.Calcular(EventosDeEstado.De([IssuedDocumentEventKind.Issued, IssuedDocumentEventKind.Settled]), null, hoy)
            .Should().Be(IssuedDocumentStatus.Settled);
        EstadoDeDocumentoEmitido.Calcular(EventosDeEstado.De([IssuedDocumentEventKind.Superseded, IssuedDocumentEventKind.Voided]), null, hoy)
            .Should().Be(IssuedDocumentStatus.Voided);
        EstadoDeDocumentoEmitido.Calcular(EventosDeEstado.De([IssuedDocumentEventKind.Issued]), hoy.AddDays(2), hoy)
            .Should().Be(IssuedDocumentStatus.SubjectToFinality);
    }
}
