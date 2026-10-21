using FluentAssertions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Housing;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Housing;

/// <summary>
/// Feature 013, K6b (T1042, T1053, T1054; FR-024, D18): las piezas de vivienda del motor fuera de los casos dorados —la versión de reglas de
/// vivienda, la serie UVR y su proyección, las conversiones entre pesos y UVR, la causación sin UVR del día y las reglas de la Ley 546 de 1999—. Los
/// casos dorados 61–65 (<c>Casos/vivienda.py</c>) fijan el cálculo al centavo; aquí, las reglas una a una. Los valores de UVR y de los límites son de
/// prueba, no de la norma.
/// </summary>
public class ViviendaYUvrTests
{
    private static readonly RoundingMode Lejos = RoundingMode.AwayFromZero;

    [Fact]
    public void Estan_los_casos_dorados_de_vivienda_de_K6b()
    {
        CasoDoradoDeCartera.Archivos().Select(Path.GetFileNameWithoutExtension).Should().Contain(
        [
            "61-vivienda-pesos-tasa-fija", "62-vivienda-uvr-cuota-constante-en-uvr", "63-vivienda-uvr-pago-y-reverso",
            "64-vivienda-sin-mora-sobre-intereses", "65-vivienda-prepago-sin-penalidad",
        ]);
        foreach (var archivo in CasoDoradoDeCartera.Archivos().Where(a => Path.GetFileName(a).StartsWith("6", StringComparison.Ordinal)))
            CasoDoradoDeCartera.Cargar(archivo).Condiciones.RulesVersion.Should().Be(VersionesDeReglas.Vivienda, Path.GetFileName(archivo));
    }

    // ------------------------------------------------------------------------------------------------- versión de reglas

    [Fact]
    public void La_version_de_vivienda_es_la_siguiente_y_los_creditos_de_K1_conservan_la_suya()
    {
        VersionesDeReglas.Vivienda.Should().Be("LND-2026.2");
        VersionesDeReglas.Conocidas.Should().Equal(VersionesDeReglas.Vigente, VersionesDeReglas.Vivienda);
        VersionesDeReglas.ParaCreditoNuevo(esVivienda: true).Should().Be(VersionesDeReglas.Vivienda);
        VersionesDeReglas.ParaCreditoNuevo(esVivienda: false).Should().Be(VersionesDeReglas.Vigente, "un crédito que no es de vivienda sella la de K1");
        VersionesDeReglas.EsDeVivienda(VersionesDeReglas.Vivienda).Should().BeTrue();
        VersionesDeReglas.EsDeVivienda(VersionesDeReglas.Vigente).Should().BeFalse();

        var vivienda = VersionesDeReglas.Para(VersionesDeReglas.Vivienda);
        vivienda.AdmiteMoraSobreIntereses.Should().BeFalse("Ley 546 de 1999: nunca mora sobre intereses en vivienda (FR-063)");
        vivienda.PrepagoSinPenalidad.Should().BeTrue();
        var k1 = VersionesDeReglas.Para(VersionesDeReglas.Vigente);
        k1.AdmiteMoraSobreIntereses.Should().BeTrue();
        k1.PrepagoSinPenalidad.Should().BeFalse("fuera de vivienda rige el tope legal de la Ley 1555 de 2012");
    }

    [Fact]
    public void Con_las_reglas_de_vivienda_la_mora_sobre_intereses_sellada_no_cambia_nada_y_con_las_de_K1_si()
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, "64-vivienda-sin-mora-sobre-intereses.json"));
        var r = DateTime.Parse("2027-02-10T23:00:00Z").ToUniversalTime();
        var dia = new DateOnly(2027, 2, 10);
        var conMora = caso.Entrada(caso.Condiciones, caso.Operaciones, r);
        conMora.Condiciones.MoraSobreIntereses.Should().BeTrue("el caso la trae encendida a propósito");
        var sinMora = conMora with { Condiciones = conMora.Condiciones with { MoraSobreIntereses = false } };

        MotorDeCartera.Reconstruir(conMora, dia).Saldos.Should().BeEquivalentTo(MotorDeCartera.Reconstruir(sinMora, dia).Saldos,
            "con las reglas de vivienda la mora corre sólo sobre el capital vencido");

        // Las mismas condiciones con las reglas de K1 (y el tope de vivienda como usura) sí causan mora sobre el interés vencido.
        var k1 = conMora with { Condiciones = conMora.Condiciones with { RulesVersion = VersionesDeReglas.Vigente }, Usura = conMora.TopeDeVivienda };
        var k1Sin = k1 with { Condiciones = k1.Condiciones with { MoraSobreIntereses = false } };
        MotorDeCartera.Reconstruir(k1, dia).Saldo(LoanConcept.DefaultInterest).Should()
            .BeGreaterThan(MotorDeCartera.Reconstruir(k1Sin, dia).Saldo(LoanConcept.DefaultInterest));
    }

    [Fact]
    public void El_tope_del_dia_de_vivienda_es_el_menor_entre_el_tope_y_la_usura()
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, "62-vivienda-uvr-cuota-constante-en-uvr.json"));
        var r = DateTime.Parse("2026-11-18T23:00:00Z").ToUniversalTime();
        var dia = new DateOnly(2026, 11, 18);
        var entrada = caso.Entrada(caso.Condiciones, caso.Operaciones, r);
        var soloTope = MotorDeCartera.Reconstruir(entrada, dia).TasaDelDia!;

        // Una usura más baja que el tope rige; una más alta, no.
        var baja = entrada with { Usura = [new ValorDeUsura(new DateOnly(2026, 1, 1), 0.06m, r.AddYears(-1), 1, Guid.NewGuid())] };
        var alta = entrada with { Usura = [new ValorDeUsura(new DateOnly(2026, 1, 1), 0.30m, r.AddYears(-1), 1, Guid.NewGuid())] };
        MotorDeCartera.Reconstruir(baja, dia).TasaDelDia!.CorrienteTopada.Should().BeTrue("la usura de 6 % es menor que la corriente de 7,5 %");
        MotorDeCartera.Reconstruir(alta, dia).TasaDelDia!.TasaDiariaMora.Should().Be(soloTope.TasaDiariaMora, "rige el tope de vivienda, que es menor");
        ReglasDeVivienda.TopeDelDia(0.105m, 0.30m).Should().Be(0.105m);
        ReglasDeVivienda.TopeDelDia(0.105m, 0.06m).Should().Be(0.06m);
        ReglasDeVivienda.TopeDelDia(null, null).Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------------- serie UVR

    [Fact]
    public void La_UVR_del_dia_es_la_publicada_y_sin_publicar_se_proyecta_con_el_factor_de_sus_dos_ultimos_dias()
    {
        var serie = new SerieUvr([new ValorDeUvr(new DateOnly(2027, 2, 14), 400.0000m), new ValorDeUvr(new DateOnly(2027, 2, 15), 400.1000m)]);
        serie.Del(new DateOnly(2027, 2, 15)).Should().Be(new UvrDelDia(new DateOnly(2027, 2, 15), 400.1000m, false, new DateOnly(2027, 2, 15)));

        // Factor 400,1 / 400 = 1,00025; tres días después: 400,1 × 1,00025³ = 400,40015…, a la diezmilésima.
        var proyectada = serie.Del(new DateOnly(2027, 2, 18));
        proyectada.Proyectada.Should().BeTrue("sólo para mostrar");
        proyectada.UltimaPublicada.Should().Be(new DateOnly(2027, 2, 15));
        proyectada.Valor.Should().Be(400.4002m);

        serie.Publicada(new DateOnly(2027, 2, 18)).Should().BeNull();
        var antes = () => serie.Del(new DateOnly(2027, 1, 1));
        antes.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(SerieUvr.CodigoFaltante);
        var repetida = () => new SerieUvr([new ValorDeUvr(new DateOnly(2027, 2, 14), 1m), new ValorDeUvr(new DateOnly(2027, 2, 14), 2m)]);
        repetida.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Sin_UVR_publicada_del_dia_el_credito_en_UVR_no_se_causa_y_responde_NationalValues_Missing()
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, "62-vivienda-uvr-cuota-constante-en-uvr.json"));
        var r = DateTime.Parse("2027-02-20T23:00:00Z").ToUniversalTime();
        var entrada = caso.Entrada(caso.Condiciones, caso.Operaciones, r);
        // La serie de prueba llega hasta el 15-02-2027.
        MotorDeCartera.Causar(entrada, new DateOnly(2027, 2, 14), new DateOnly(2027, 2, 15)).Causo.Should().BeTrue();
        var sinUvr = MotorDeCartera.Causar(entrada, new DateOnly(2027, 2, 15), new DateOnly(2027, 2, 16));
        sinUvr.Causo.Should().BeFalse();
        sinUvr.Problema!.Codigo.Should().Be("NationalValues.Missing");
        sinUvr.Filas.Should().BeEmpty("la marca no avanza");

        // La liquidación a esa fecha sí se muestra, con la UVR proyectada.
        MotorDeCartera.Reconstruir(entrada, new DateOnly(2027, 2, 16)).Uvr!.Proyectada.Should().BeTrue();
    }

    [Fact]
    public void La_causacion_de_un_credito_en_UVR_lleva_su_correccion_monetaria_y_el_libro_cuadra_con_los_saldos_en_pesos()
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, "62-vivienda-uvr-cuota-constante-en-uvr.json"));
        var r = DateTime.Parse("2026-12-20T23:00:00Z").ToUniversalTime();
        var entrada = caso.Entrada(caso.Condiciones, caso.Operaciones, r);
        var dia = MotorDeCartera.Causar(entrada, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 10));
        dia.Filas.Should().Contain(f => f.Clase == LoanTransactionKind.MonetaryCorrection && f.Concepto == LoanConcept.Principal && f.Cuota == null && f.Monto > 0m,
            "la UVR sube y el capital en pesos con ella");
        dia.Filas.Should().Contain(f => f.Clase == LoanTransactionKind.Accrual && f.Concepto == LoanConcept.CurrentInterest);

        var estado = MotorDeCartera.Reconstruir(entrada, new DateOnly(2026, 12, 20), new DateOnly(2026, 11, 3));
        estado.Libro.Movimientos.Where(m => m.Concepto == LoanConcept.Principal).Sum(m => m.Monto).Should().Be(estado.Saldo(LoanConcept.Principal));
        estado.Saldo(LoanConcept.Principal).Should().Be(ConversionUvr.APesos(estado.Uvr!.CapitalEnUvr, estado.Uvr.Valor, Lejos),
            "el capital en pesos es la valoración del capital total en UVR a la UVR del día");
        MotorDeCartera.Verificar(entrada, new DateOnly(2026, 12, 20), estado.Libro.Movimientos).Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------------- conversiones

    [Fact]
    public void Las_conversiones_redondean_el_valor_absoluto_con_el_modo_de_la_linea()
    {
        ConversionUvr.AUvr(1_000_000m, 400.1234m, Lejos).Should().Be(2499.2290m);
        ConversionUvr.AUvr(-1_000_000m, 400.1234m, Lejos).Should().Be(-2499.2290m);
        ConversionUvr.APesos(2499.2289m, 400.1234m, Lejos).Should().Be(999_999.96m);
        ConversionUvr.APesos(-0.00004m, 400m, RoundingMode.Up).Should().Be(-0.02m, "el signo no cambia el redondeo: |−0,016| hacia arriba es 0,02");
        var cero = () => ConversionUvr.AUvr(1m, 0m, Lejos);
        cero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Repartir_por_acumulado_cuadra_contra_el_total_en_el_ultimo_distinto_de_cero()
    {
        // 3 × 0,3333 UVR a 3 pesos: uno a uno serían 1,00 + 1,00 + 1,00; por acumulado 1,00 + 1,00 + 1,00 (= U(0,9999 × 3) = 3,00).
        ConversionUvr.Repartir([0.3333m, 0.3333m, 0.3333m], 3m, Lejos).Should().Equal(1.00m, 1.00m, 1.00m);
        // Con total 3,01: lo absorbe el último distinto de cero (el cuarto es cero).
        ConversionUvr.Repartir([0.3333m, 0.3333m, 0.3333m, 0m], 3m, Lejos, 3.01m).Should().Equal(1.00m, 1.00m, 1.01m, 0m);
    }

    // ------------------------------------------------------------------------------------------------- Ley 546

    private static LimitesDeVivienda Limites() => new(
        new LimiteVigente(CatalogoDeLimitesLegales.ViviendaPlazoMinimoMeses, 60m, "Ley 546 de 1999, art. 17"),
        new LimiteVigente(CatalogoDeLimitesLegales.ViviendaPlazoMaximoMeses, 360m, "Ley 546 de 1999, art. 17"),
        new LimiteVigente(CatalogoDeLimitesLegales.ViviendaFinanciable, 0.70m, "Ley 546 de 1999, art. 17"),
        new LimiteVigente(CatalogoDeLimitesLegales.ViviendaFinanciableVis, 0.80m, "Ley 546 de 1999, art. 17"),
        new LimiteVigente(CatalogoDeLimitesLegales.ViviendaCuotaIngreso, 0.30m, "Ley 546 de 1999, art. 17"));

    [Fact]
    public void La_linea_de_vivienda_es_de_tasa_fija_bajo_el_tope_y_con_plazo_dentro_de_los_limites()
    {
        var tope = new TopeDeTasaDeVivienda(0.12m, Guid.NewGuid(), new DateOnly(2026, 1, 1), "Junta Directiva del Banco de la República (prueba)");
        ReglasDeVivienda.ValidarLinea(new LineaDeVivienda(RateKind.Fixed, 0.11m, 60, 240, DenominationUnit.Uvr, true), tope, null, Limites()).Should().BeEmpty();

        ReglasDeVivienda.ValidarLinea(new LineaDeVivienda(RateKind.Variable, null, 60, 240, DenominationUnit.Pesos, true), tope, null, Limites())
            .Select(p => p.Codigo).Should().Equal(ReglasDeVivienda.CodigoTasaVariable);
        var excede = ReglasDeVivienda.ValidarLinea(new LineaDeVivienda(RateKind.Fixed, 0.125m, 60, 240, DenominationUnit.Pesos, true), tope, null, Limites());
        excede.Select(p => p.Codigo).Should().Equal(ReglasDeVivienda.CodigoTopeExcedido);
        excede[0].Datos!["cap"].Should().Be(0.12m);
        excede[0].Datos!["source"].Should().Be(tope.Fuente);
        excede[0].Mensaje.Should().Contain("Banco de la República");
        ReglasDeVivienda.ValidarLinea(new LineaDeVivienda(RateKind.Fixed, 0.11m, 60, 240, DenominationUnit.Pesos, true), tope, 0.10m, Limites())
            .Select(p => p.Codigo).Should().Equal([ReglasDeVivienda.CodigoTopeExcedido], "rige el menor entre el tope y la usura");

        var plazo = ReglasDeVivienda.ValidarLinea(new LineaDeVivienda(RateKind.Fixed, 0.11m, 36, 420, DenominationUnit.Pesos, true), tope, null, Limites());
        plazo.Select(p => p.Datos!["limitCode"]).Should().Equal(CatalogoDeLimitesLegales.ViviendaPlazoMinimoMeses, CatalogoDeLimitesLegales.ViviendaPlazoMaximoMeses);
        plazo.Should().OnlyContain(p => p.Codigo == ReglasDeVivienda.CodigoPlazoFueraDeLimites && p.Mensaje.Contains("Ley 546"));

        ReglasDeVivienda.ValidarLinea(new LineaDeVivienda(RateKind.Fixed, 0.11m, 60, 240, DenominationUnit.Uvr, false), tope, null, Limites())
            .Select(p => p.Codigo).Should().Equal(ReglasDeVivienda.CodigoUvrNoAdmitida);
    }

    [Fact]
    public void La_solicitud_de_vivienda_respeta_el_porcentaje_financiable_general_y_VIS_y_la_relacion_cuota_ingreso()
    {
        ReglasDeVivienda.ValidarSolicitud(new SolicitudDeVivienda(70_000_000m, 180, 100_000_000m, false, 10_000_000m, 3_000_000m), Limites()).Should().BeEmpty();

        var general = ReglasDeVivienda.ValidarSolicitud(new SolicitudDeVivienda(75_000_000m, 180, 100_000_000m, false, 10_000_000m, 3_000_000m), Limites());
        general.Select(p => (p.Codigo, p.Datos!["limitCode"])).Should().Equal((ReglasDeVivienda.CodigoFinanciacionExcedida, CatalogoDeLimitesLegales.ViviendaFinanciable));
        ReglasDeVivienda.ValidarSolicitud(new SolicitudDeVivienda(75_000_000m, 180, 100_000_000m, true, 10_000_000m, 3_000_000m), Limites())
            .Should().BeEmpty("la VIS financia hasta el 80 %");
        var vis = ReglasDeVivienda.ValidarSolicitud(new SolicitudDeVivienda(85_000_000m, 180, 100_000_000m, true, 10_000_000m, 3_000_000m), Limites());
        vis.Single().Datos!["limitCode"].Should().Be(CatalogoDeLimitesLegales.ViviendaFinanciableVis);

        var cuota = ReglasDeVivienda.ValidarSolicitud(new SolicitudDeVivienda(70_000_000m, 180, 100_000_000m, false, 10_000_000m, 3_000_000.01m), Limites());
        cuota.Single().Codigo.Should().Be(ReglasDeVivienda.CodigoCuotaIngresoExcedida);
        cuota.Single().Datos!["allowed"].Should().Be(3_000_000m);

        ReglasDeVivienda.ValidarSolicitud(new SolicitudDeVivienda(70_000_000m, 180, null, false, null, 3_000_000m), Limites())
            .Select(p => p.Datos!["field"]).Should().Equal("propertyValue", "familyIncome");
        ReglasDeVivienda.ValidarSolicitud(new SolicitudDeVivienda(70_000_000m, 48, 100_000_000m, false, 10_000_000m, 3_000_000m), Limites())
            .Single().Codigo.Should().Be(ReglasDeVivienda.CodigoPlazoFueraDeLimites);
    }

    [Fact]
    public void El_desembolso_de_vivienda_exige_una_hipoteca_de_primer_grado()
    {
        ReglasDeVivienda.ValidarGarantias([new GarantiaDeVivienda(CollateralKind.Mortgage, 1)]).Should().BeNull();
        ReglasDeVivienda.ValidarGarantias([new GarantiaDeVivienda(CollateralKind.PromissoryNote, null), new GarantiaDeVivienda(CollateralKind.Mortgage, 1)])
            .Should().BeNull();
        ReglasDeVivienda.ValidarGarantias([new GarantiaDeVivienda(CollateralKind.Mortgage, 2)])!.Codigo.Should().Be(ReglasDeVivienda.CodigoHipotecaDePrimerGrado);
        ReglasDeVivienda.ValidarGarantias([new GarantiaDeVivienda(CollateralKind.PromissoryNote, null)])!.Mensaje.Should().Contain("registre la hipoteca");
    }

    [Fact]
    public void Un_pago_que_alcanza_el_valor_para_cancelar_en_pesos_cancela_exactamente_lo_que_se_debe_en_UVR()
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, "62-vivienda-uvr-cuota-constante-en-uvr.json"));
        var r = DateTime.Parse("2026-12-10T23:00:00Z").ToUniversalTime();
        var estado = MotorDeCartera.Reconstruir(caso.Entrada(caso.Condiciones, caso.Operaciones, r), new DateOnly(2026, 12, 10));
        var credito = caso.Condiciones.Credito(VersionesDeReglas.Vivienda);
        var exacto = MotorDeCartera.DistribuirPago(estado, new SolicitudDeDistribucion(PaymentModality.FullPayoff, estado.ValorParaCancelar, new DateOnly(2026, 12, 10)),
            credito.Prelacion, credito.Redondeo);
        exacto.Cancela.Should().BeTrue("pagar lo que la pantalla muestra en pesos no deja una diezmilésima pendiente");
        exacto.Excedente.Should().Be(0m);
        (exacto.Lineas.Sum(l => l.Monto) + exacto.Anticipo + exacto.SaldoAFavor + exacto.Excedente - exacto.AnticipoUsado.Sum(u => u.Monto))
            .Should().Be(estado.ValorParaCancelar, "Valor = Σ Líneas + Anticipo + SaldoAFavor + Excedente − Σ AnticipoUsado, al centavo");
    }
}
