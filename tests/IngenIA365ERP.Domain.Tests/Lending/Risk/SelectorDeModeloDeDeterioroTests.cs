using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9 (T0673; T50, R58; FR-105, D28): <see cref="SelectorDeModeloDeDeterioro"/> por segmento de pérdida esperada. No
/// obligada → <c>ArrearsAnnex1</c>/<c>NotObligated</c>; obligada sin K6a → <c>ArrearsAnnex1</c> con la marca; la disponibilidad de K6a
/// la decide <see cref="EntregaDeCartera"/>, nunca una bandera.
/// </summary>
public class SelectorDeModeloDeDeterioroTests
{
    private static readonly ReglaDePerdidaEsperada Regla = CasoDoradoDeRiesgo.LeerRegla("regla-prueba");

    [Fact]
    public void La_entrega_desplegada_hoy_trae_la_perdida_esperada()
    {
        // T1024 (fase 22, 2026-10-16): la entrega vigente subió a K6a; antes de ella el selector deja la marca (r15, D28).
        SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(CatalogoDeCartera.EntregaVigente).Should().BeTrue();
        SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(EntregaDeCartera.K5).Should().BeFalse();
        SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(EntregaDeCartera.K6a).Should().BeTrue();
        SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(EntregaDeCartera.K6b).Should().BeTrue();
    }

    [Fact]
    public void Una_no_obligada_sigue_el_Anexo_1_sin_marca()
    {
        var s = SelectorDeModeloDeDeterioro.Seleccionar(Regla, new("COOP_MULTIACTIVA", null), null, EntregaDeCartera.K1);

        s.Obligada.Should().BeFalse();
        s.ModeloObligatorioNoDisponible.Should().BeFalse();
        s.Segmentos.Should().OnlyContain(m => m.Modelo == ImpairmentModel.ArrearsAnnex1 && m.Fundamento == ModelBasis.NotObligated);
        s.Segmentos.Select(m => m.Segmento).Should().Equal(ReglaDePerdidaEsperada.SegmentosConocidos);
    }

    [Fact]
    public void Una_obligada_sin_K6a_cierra_con_el_Anexo_1_y_la_marca()
    {
        var s = SelectorDeModeloDeDeterioro.Seleccionar(Regla, new("COOP_AHORRO_CREDITO", null), null, EntregaDeCartera.K1);

        s.Obligada.Should().BeTrue();
        s.ModeloObligatorioNoDisponible.Should().BeTrue();
        s.Segmentos.Should().OnlyContain(m => m.Modelo == ImpairmentModel.ArrearsAnnex1 && m.Fundamento == ModelBasis.Obligated && m.ModeloObligatorioNoDisponible);
    }

    [Fact]
    public void Con_K6a_la_obligada_va_a_perdida_esperada_en_los_segmentos_cubiertos()
    {
        var s = SelectorDeModeloDeDeterioro.Seleccionar(Regla, new("COOP_AHORRO_CREDITO", null), null, EntregaDeCartera.K6a);

        s.ModeloObligatorioNoDisponible.Should().BeFalse();
        s.Segmentos.Should().OnlyContain(m => m.Modelo == ImpairmentModel.ExpectedLossAnnex2 && m.Fundamento == ModelBasis.Obligated);
        s.ParaCredito("CONSUMO_LIBRANZA").Modelo.Should().Be(ImpairmentModel.ExpectedLossAnnex2);
        s.ParaCredito(null).Should().Be(new ModeloDelSegmento(string.Empty, ImpairmentModel.ArrearsAnnex1, ModelBasis.NotObligated, false),
            "vivienda, microcrédito y comercial de persona jurídica no tienen segmento de pérdida esperada");
    }

    [Theory]
    [InlineData("PLENA", true)]
    [InlineData("INTERMEDIA", false)]
    [InlineData(null, true)]
    public void Un_fondo_de_empleados_esta_obligado_segun_su_categoria(string? categoria, bool obligada)
    {
        Regla.Obliga("FONDO_EMPLEADOS", categoria).Should().Be(obligada);
        SelectorDeModeloDeDeterioro.Seleccionar(Regla, new("FONDO_EMPLEADOS", categoria), null, EntregaDeCartera.K1).ModeloObligatorioNoDisponible
            .Should().Be(obligada);
    }

    [Fact]
    public void Sin_regla_ni_perfil_nadie_esta_obligado()
    {
        SelectorDeModeloDeDeterioro.Seleccionar(null, new("COOP_AHORRO_CREDITO", null), null, EntregaDeCartera.K1).Obligada.Should().BeFalse();
        SelectorDeModeloDeDeterioro.Seleccionar(Regla, null, null, EntregaDeCartera.K1).Obligada.Should().BeFalse();
        Regla.Obliga(null, null).Should().BeFalse("un perfil sin registrar no se presume obligado");
    }

    [Fact]
    public void La_adopcion_voluntaria_solo_cuenta_con_K6a_y_el_desistimiento_vuelve_al_Anexo_1()
    {
        var adopcion = new EleccionDeModelo(ImpairmentModelElectionKind.VoluntaryAdoption, ["CONSUMO_LIBRANZA"]);
        var perfil = new PerfilParaModelo("COOP_MULTIACTIVA", null);

        SelectorDeModeloDeDeterioro.Seleccionar(Regla, perfil, adopcion, EntregaDeCartera.K1).ParaCredito("CONSUMO_LIBRANZA").Modelo
            .Should().Be(ImpairmentModel.ArrearsAnnex1, "sin K6a ninguna cooperativa adopta el modelo");
        var conK6a = SelectorDeModeloDeDeterioro.Seleccionar(Regla, perfil, adopcion, EntregaDeCartera.K6a);
        conK6a.ParaCredito("CONSUMO_LIBRANZA").Should().Be(new ModeloDelSegmento("CONSUMO_LIBRANZA", ImpairmentModel.ExpectedLossAnnex2, ModelBasis.Voluntary, false));
        conK6a.ParaCredito("COMERCIAL_PN").Modelo.Should().Be(ImpairmentModel.ArrearsAnnex1, "la adopción cubre sólo sus segmentos");

        var desistimiento = new EleccionDeModelo(ImpairmentModelElectionKind.VoluntaryWithdrawal, ["CONSUMO_LIBRANZA"]);
        SelectorDeModeloDeDeterioro.Seleccionar(Regla, perfil, desistimiento, EntregaDeCartera.K6a).ParaCredito("CONSUMO_LIBRANZA").Modelo
            .Should().Be(ImpairmentModel.ArrearsAnnex1);
    }

    // --------------------------------------------------------------------------------------------- K6a (T1014) --

    private static readonly IReadOnlyList<string> Los3 = ReglaDePerdidaEsperada.SegmentosConocidos;

    [Fact]
    public void Con_K6a_la_obligada_va_a_perdida_esperada_en_los_tres_segmentos_y_al_Anexo_1_en_los_demas_sin_marca()
    {
        var s = SelectorDeModeloDeDeterioro.SeleccionarAl(new DateOnly(2026, 10, 31), Regla,
            [new VigenciaDePerfil(new DateOnly(2026, 1, 1), new("COOP_AHORRO_CREDITO", null))], [], EntregaDeCartera.K6a);

        s.ModeloObligatorioNoDisponible.Should().BeFalse();
        s.Segmentos.Should().OnlyContain(m => m.Modelo == ImpairmentModel.ExpectedLossAnnex2 && m.Fundamento == ModelBasis.Obligated && !m.ModeloObligatorioNoDisponible);
        s.ParaCredito(null).Should().Be(new ModeloDelSegmento(string.Empty, ImpairmentModel.ArrearsAnnex1, ModelBasis.NotObligated, false));
    }

    [Fact]
    public void Sin_K6a_la_obligada_sigue_como_en_r15_con_la_marca()
    {
        var s = SelectorDeModeloDeDeterioro.SeleccionarAl(new DateOnly(2026, 10, 31), Regla,
            [new VigenciaDePerfil(new DateOnly(2026, 1, 1), new("COOP_AHORRO_CREDITO", null))], [], EntregaDeCartera.K5);

        s.Segmentos.Should().OnlyContain(m => m.Modelo == ImpairmentModel.ArrearsAnnex1 && m.ModeloObligatorioNoDisponible);
    }

    [Fact]
    public void La_adopcion_voluntaria_rige_desde_su_fecha_y_el_desistimiento_desde_la_suya_sin_tocar_cierres_anteriores()
    {
        var perfil = new[] { new VigenciaDePerfil(new DateOnly(2026, 1, 1), new("COOP_MULTIACTIVA", null)) };
        var elecciones = new[]
        {
            new EleccionDeModelo(ImpairmentModelElectionKind.VoluntaryAdoption, Los3) { Desde = new DateOnly(2026, 11, 30) },
            new EleccionDeModelo(ImpairmentModelElectionKind.VoluntaryWithdrawal, Los3) { Desde = new DateOnly(2027, 6, 30) },
        };
        ModeloAl(new DateOnly(2026, 10, 31)).Should().Be((ImpairmentModel.ArrearsAnnex1, ModelBasis.NotObligated), "el cierre anterior a la adopción no cambia");
        ModeloAl(new DateOnly(2026, 11, 30)).Should().Be((ImpairmentModel.ExpectedLossAnnex2, ModelBasis.Voluntary), "desde su ValidFrom");
        ModeloAl(new DateOnly(2027, 5, 31)).Should().Be((ImpairmentModel.ExpectedLossAnnex2, ModelBasis.Voluntary));
        ModeloAl(new DateOnly(2027, 6, 30)).Should().Be((ImpairmentModel.ArrearsAnnex1, ModelBasis.NotObligated), "el desistimiento rige desde su fecha");

        (ImpairmentModel, ModelBasis) ModeloAl(DateOnly cierre)
        {
            var m = SelectorDeModeloDeDeterioro.SeleccionarAl(cierre, Regla, perfil, elecciones, EntregaDeCartera.K6a).ParaCredito("CONSUMO_SIN_LIBRANZA");
            return (m.Modelo, m.Fundamento);
        }
    }

    [Fact]
    public void La_obligacion_sobrevenida_por_el_perfil_rige_desde_el_primer_cierre_dentro_de_su_vigencia()
    {
        var perfiles = new[]
        {
            new VigenciaDePerfil(new DateOnly(2026, 1, 1), new("COOP_MULTIACTIVA", null)),
            new VigenciaDePerfil(new DateOnly(2027, 1, 1), new("COOP_MULTIACTIVA_SECCION_AC", null)),
        };

        SelectorDeModeloDeDeterioro.SeleccionarAl(new DateOnly(2026, 12, 31), Regla, perfiles, [], EntregaDeCartera.K6a).Obligada.Should().BeFalse();
        var enero = SelectorDeModeloDeDeterioro.SeleccionarAl(new DateOnly(2027, 1, 31), Regla, perfiles, [], EntregaDeCartera.K6a);
        enero.Obligada.Should().BeTrue();
        enero.Segmentos.Should().OnlyContain(m => m.Modelo == ImpairmentModel.ExpectedLossAnnex2 && m.Fundamento == ModelBasis.Obligated);
    }

    [Fact]
    public void Una_obligada_con_una_eleccion_registrada_sigue_obligada()
    {
        var s = SelectorDeModeloDeDeterioro.SeleccionarAl(new DateOnly(2026, 10, 31), Regla,
            [new VigenciaDePerfil(new DateOnly(2026, 1, 1), new("FONDO_EMPLEADOS", "PLENA"))],
            [new EleccionDeModelo(ImpairmentModelElectionKind.VoluntaryWithdrawal, Los3) { Desde = new DateOnly(2026, 1, 31) }], EntregaDeCartera.K6a);

        s.Segmentos.Should().OnlyContain(m => m.Fundamento == ModelBasis.Obligated && m.Modelo == ImpairmentModel.ExpectedLossAnnex2,
            "una obligada no desiste: la elección no la toca (Lending.ImpairmentModel.Obligated la niega al registrarla)");
    }
}
