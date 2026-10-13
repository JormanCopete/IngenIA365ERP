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
    public void La_entrega_desplegada_hoy_no_trae_la_perdida_esperada()
    {
        SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(CatalogoDeCartera.EntregaVigente).Should().BeFalse();
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
}
