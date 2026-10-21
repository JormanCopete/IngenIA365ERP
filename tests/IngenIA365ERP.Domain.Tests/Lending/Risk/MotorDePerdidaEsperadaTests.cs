using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, K6a (T1029, T1030): lo que <see cref="MotorDePerdidaEsperada"/> y <see cref="HomologacionDeCalificacion.DePerdidaEsperada"/> hacen
/// fuera de los casos dorados pe01–pe10: se niegan cuando el Anexo 2 vigente no trae la PI (la voluntaria de la CE 100, o la de un tipo de
/// organización que sus tablas no tienen) en vez de inventarla; el pago único con más días que su umbral está incumplido; la alineación por
/// deudor y modalidad; y el redondeo del puntaje, que es el único (E9).
/// </summary>
public class MotorDePerdidaEsperadaTests
{
    private static readonly Anexo2 Sembrado = CasoDoradoDeRiesgo.LeerAnexo2("sembrado");

    private static readonly SaldosParaDeterioro Saldos = new(1_000_000m, 0m, 0m, 0m);

    private static CreditoParaPuntaje AlDia(string segmento, int dias = 0) =>
        new(1, segmento, dias, Enumerable.Range(1, 12).Select(m => new MoraMensualCompleta(2026, m, 0, false)).ToList()) { AsociadoActivo = true };

    private static ContextoDelModelo Contexto(string tipo, bool voluntaria = false) => new(tipo, false, 1_750_905m, voluntaria);

    [Fact]
    public void Sin_la_PI_voluntaria_en_el_Anexo_2_vigente_el_motor_se_niega()
    {
        var contexto = Contexto("COOP_MULTIACTIVA", voluntaria: true);
        var p = MotorDePerdidaEsperada.Puntuar(Sembrado, AlDia("CONSUMO_LIBRANZA"), contexto);

        var accion = () => MotorDePerdidaEsperada.Calcular(Sembrado, new CreditoParaPerdidaEsperada(p, RatingCategory.A, Saldos, false, "SIN_GARANTIA"),
            contexto, ContextoDeAportes.SinAportes, RedondeoDeDeterioro.Centavo);

        accion.Should().Throw<InvalidOperationException>().WithMessage("*VOLUNTARIA*");
    }

    [Fact]
    public void Sin_la_PI_del_tipo_de_organizacion_el_motor_se_niega()
    {
        var contexto = Contexto("COOP_INTEGRAL_SECCION_AC");
        var p = MotorDePerdidaEsperada.Puntuar(Sembrado, AlDia("CONSUMO_LIBRANZA"), contexto);

        var accion = () => MotorDePerdidaEsperada.Calcular(Sembrado, new CreditoParaPerdidaEsperada(p, RatingCategory.A, Saldos, false, "SIN_GARANTIA"),
            contexto, ContextoDeAportes.SinAportes, RedondeoDeDeterioro.Centavo);

        accion.Should().Throw<InvalidOperationException>().WithMessage("*COOP_INTEGRAL_SECCION_AC*");
    }

    [Fact]
    public void Sin_la_PDI_del_tipo_de_garantia_el_motor_se_niega()
    {
        var contexto = Contexto("COOP_AHORRO_CREDITO");
        var p = MotorDePerdidaEsperada.Puntuar(Sembrado, AlDia("CONSUMO_LIBRANZA"), contexto);

        var sinPdi = () => MotorDePerdidaEsperada.Calcular(Sembrado, new CreditoParaPerdidaEsperada(p, RatingCategory.A, Saldos, false, "AVAL"),
            contexto, ContextoDeAportes.SinAportes, RedondeoDeDeterioro.Centavo);
        sinPdi.Should().Throw<InvalidOperationException>().WithMessage("*AVAL*");
    }

    [Fact]
    public void Los_aportes_a_prorrata_se_restan_del_VEA_con_las_condiciones_del_numeral_5_1()
    {
        // Decisión del dueño del 2026-10-18 (feature 014, D39): Anexo 2 num. 4.2 —el VEA descuenta los aportes según el num. 5.1 del Anexo 1— y 4.3 —los
        // aportes no entran a la PDI—. Hasta esa fecha el motor no los descontaba (E62).
        var contexto = Contexto("COOP_AHORRO_CREDITO");
        var p = MotorDePerdidaEsperada.Puntuar(Sembrado, AlDia("CONSUMO_LIBRANZA"), contexto);
        var conSaldos = new SaldosParaDeterioro(1_000_000m, 50_000m, 0m, 0m);
        PerdidaEsperadaDeCredito Calcular(ContextoDeAportes aportes, decimal aProrrata, bool normaAdmite = true) =>
            MotorDePerdidaEsperada.Calcular(Sembrado,
                new CreditoParaPerdidaEsperada(p, RatingCategory.A, conSaldos, false, "SIN_GARANTIA") { AportesAProrrata = aProrrata, NormaAdmiteAportes = normaAdmite },
                contexto, aportes, RedondeoDeDeterioro.Centavo);

        var sin = Calcular(ContextoDeAportes.SinAportes, 391_000m);
        var con = Calcular(new ContextoDeAportes(true, true, true), 391_000m);

        sin.Exposicion.Should().Be(1_050_000m);
        sin.AportesDescontados.Should().Be(0m);
        sin.Explicacion.Texto.Should().Contain("Riesgo.DescuentoAportesDeterioro no está habilitado");
        con.AportesDescontados.Should().Be(391_000m);
        con.ExposicionCapital.Should().Be(609_000m, "los aportes se restan primero del capital");
        con.ExposicionIntereses.Should().Be(50_000m);
        con.Exposicion.Should().Be(659_000m);
        con.PerdidaDadoElIncumplimiento.Should().Be(sin.PerdidaDadoElIncumplimiento, "los aportes no entran a la PDI (num. 4.3)");
        con.Total.Should().BeLessThan(sin.Total);
        con.Explicacion.Texto.Should().Contain("Anexo 2 num. 4.2");

        var cubierto = Calcular(new ContextoDeAportes(true, true, true), 2_000_000m);
        cubierto.Exposicion.Should().Be(0m);
        cubierto.Total.Should().Be(0m, "aportes que cubren todo el VEA dejan el deterioro en cero (guía de la Supersolidaria, §3.2 n)");

        Calcular(new ContextoDeAportes(true, true, false), 391_000m).AportesDescontados.Should().Be(0m, "sin indicadores de la entidad no se descuentan");
        Calcular(new ContextoDeAportes(true, true, true), 391_000m, normaAdmite: false).AportesDescontados.Should().Be(0m);
    }

    [Fact]
    public void El_pago_unico_con_mora_mayor_que_su_umbral_esta_incumplido()
    {
        var p = MotorDePerdidaEsperada.Puntuar(Sembrado, AlDia("CONSUMO_SIN_LIBRANZA", 8) with { PagoUnico = true }, Contexto("COOP_AHORRO_CREDITO"));

        p.Incumplido.Should().BeTrue("mora superior a 7 días en un crédito a una sola cuota");
        p.DiasEnIncumplimiento.Should().Be(1);
        p.Z.Should().BeNull();
        p.CandidataParaCalificar.Should().BeNull();
        MotorDePerdidaEsperada.Puntuar(Sembrado, AlDia("CONSUMO_SIN_LIBRANZA", 7) with { PagoUnico = true }, Contexto("COOP_AHORRO_CREDITO"))
            .Incumplido.Should().BeFalse();

        var r = MotorDePerdidaEsperada.Calcular(Sembrado, new CreditoParaPerdidaEsperada(p, RatingCategory.A, Saldos, false, "SIN_GARANTIA"),
            Contexto("COOP_AHORRO_CREDITO"), ContextoDeAportes.SinAportes, RedondeoDeDeterioro.Centavo);
        r.ProbabilidadDeIncumplimiento.Should().Be(1m);
        r.PerdidaDadoElIncumplimiento.Should().Be(0.75m);
        r.CalificacionContable.Should().Be(RatingCategory.D, "sin fila para 8 días, el incumplido toma la primera fila de su homologación (lectura del equipo)");
    }

    [Fact]
    public void La_alineacion_lleva_al_incumplimiento_los_demas_creditos_del_deudor_en_la_misma_modalidad()
    {
        var creditos = new[]
        {
            new CreditoParaAlinear(1, 10, "CONSUMO", 120, true),
            new CreditoParaAlinear(2, 10, "CONSUMO", 0, false),
            new CreditoParaAlinear(3, 10, "COMERCIAL_PN", 0, false),
            new CreditoParaAlinear(4, 20, "CONSUMO", 15, false),
        };

        MotorDePerdidaEsperada.IncumplidosPorArrastre(creditos).Should().BeEquivalentTo([2]);
        MotorDePerdidaEsperada.MoraActualPorDeudor(creditos).Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 120, [2] = 120, [3] = 0, [4] = 15 });
    }

    [Fact]
    public void Un_credito_incumplido_por_arrastre_usa_la_PI_del_incumplimiento_y_la_PDI_desde_cero_dias()
    {
        var contexto = Contexto("COOP_AHORRO_CREDITO");
        var p = MotorDePerdidaEsperada.Puntuar(Sembrado, AlDia("CONSUMO_LIBRANZA"), contexto);

        var r = MotorDePerdidaEsperada.Calcular(Sembrado,
            new CreditoParaPerdidaEsperada(p, RatingCategory.D, Saldos, false, "NO_IDONEA") { IncumplidoPorArrastre = true, MoraActualDelDeudor = 120 },
            contexto, ContextoDeAportes.SinAportes, RedondeoDeDeterioro.Peso);

        r.Incumplido.Should().BeTrue();
        r.ProbabilidadDeIncumplimiento.Should().Be(1m);
        r.PerdidaDadoElIncumplimiento.Should().Be(0.60m);
        r.DeterioroCapital.Should().Be(600_000m);
        r.CalificacionContable.Should().Be(RatingCategory.D, "incumplido con mora actual del deudor de 120 días");
        r.Explicacion.Texto.Should().Contain("Incumplido por arrastre");
    }

    [Fact]
    public void El_puntaje_se_redondea_a_la_escala_del_documento_y_el_corte_es_inclusive()
    {
        // EA = 1 y MORA1260 = 1 en libranza: Z = −0,542; 1/(1+e^0,542) = 0,367722… → 0,3677 = el corte de C. Sin el redondeo de E9 sería D.
        var historial = Enumerable.Range(1, 12).Select(m => new MoraMensualCompleta(2026, m, m == 10 ? 75 : 0, false)).ToList();
        var p = MotorDePerdidaEsperada.Puntuar(Sembrado, new CreditoParaPuntaje(1, "CONSUMO_LIBRANZA", 0, historial) { AsociadoActivo = true },
            Contexto("COOP_AHORRO_CREDITO"));

        p.Z.Should().Be(-0.542m);
        p.Puntaje.Should().Be(0.3677m);
        p.Calificacion.Should().Be(RatingCategory.C);
        FuncionLogistica.Evaluar(-0.542m).Should().BeGreaterThan(0.3677m);
    }

    [Fact]
    public void La_homologacion_solo_procede_si_la_maxima_es_la_del_modelo()
    {
        HomologacionDeCalificacion.DePerdidaEsperada(Sembrado, "COMERCIAL_PN", RatingCategory.C, RatingCategory.C, false, 31).Should()
            .Be((RatingCategory.C, true, "Homologación: modelo C con mora actual del deudor de 31 días → C."));
        HomologacionDeCalificacion.DePerdidaEsperada(Sembrado, "COMERCIAL_PN", RatingCategory.E, RatingCategory.E, false, 0).Contable.Should().Be(RatingCategory.C);
        HomologacionDeCalificacion.DePerdidaEsperada(Sembrado, "COMERCIAL_PN", RatingCategory.D, RatingCategory.B, false, 0).Should()
            .Match<(RatingCategory Contable, bool Homologada, string Detalle)>(h => h.Contable == RatingCategory.D && !h.Homologada);
        HomologacionDeCalificacion.DePerdidaEsperada(Sembrado, "COMERCIAL_PN", RatingCategory.D, null, true, 151).Contable.Should().Be(RatingCategory.E);
    }
}
