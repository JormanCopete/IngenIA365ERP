using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, K3 (T0896; FR-095, US14-1, C98): <see cref="RequisitosDeCastigo"/>. Lo que exige la norma —el deudor ya no es asociado, los
/// aportes cruzados, informe motivado y concepto de irrecuperabilidad— y lo que fija la política —deterioro total, altura mínima de mora y
/// gestiones (en K3 las acredita el informe)—, cada requisito con su resultado y su evidencia, y la lista de lo que falta.
/// </summary>
public class RequisitosDeCastigoTests
{
    private static readonly Guid Informe = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Concepto = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static readonly PoliticaDeCastigo Politica = new(ExigeDeterioroTotal: true, DiasMinimosDeMora: 360, GestionesMinimas: 0);

    private static CreditoParaCastigar QueCumple() =>
        new(7, DeudorEsAsociado: false, Saldo: 1_500_000m, DeterioroDelUltimoCierre: 1_500_000m, DiasDeMora: 400)
        {
            AportesCruzados = true,
            InformeMotivado = Informe,
            ConceptoDeIrrecuperabilidad = Concepto,
        };

    [Fact]
    public void Un_credito_que_cumple_todo_no_tiene_faltantes()
    {
        var v = RequisitosDeCastigo.Verificar(QueCumple(), Politica);

        v.Cumple.Should().BeTrue();
        v.Faltantes.Should().BeEmpty();
        v.Problema.Should().BeNull();
        v.Requisitos.Select(r => (r.Clave, r.Origen)).Should().Equal(
            (RequisitosDeCastigo.NoEsAsociado, OrigenDeRequisitoDeCastigo.Norma),
            (RequisitosDeCastigo.AportesCruzados, OrigenDeRequisitoDeCastigo.Norma),
            (RequisitosDeCastigo.InformeMotivado, OrigenDeRequisitoDeCastigo.Norma),
            (RequisitosDeCastigo.ConceptoDeIrrecuperabilidad, OrigenDeRequisitoDeCastigo.Norma),
            (RequisitosDeCastigo.DeterioroTotal, OrigenDeRequisitoDeCastigo.Politica),
            (RequisitosDeCastigo.AlturaMinima, OrigenDeRequisitoDeCastigo.Politica),
            (RequisitosDeCastigo.GestionesAgotadas, OrigenDeRequisitoDeCastigo.Politica));
    }

    [Fact]
    public void La_norma_el_deudor_asociado_los_aportes_sin_cruzar_y_los_soportes_que_faltan()
    {
        var v = RequisitosDeCastigo.Verificar(QueCumple() with
        {
            DeudorEsAsociado = true,
            AportesCruzados = false,
            InformeMotivado = null,
            ConceptoDeIrrecuperabilidad = null,
        }, Politica);

        v.Cumple.Should().BeFalse();
        v.Faltantes.Should().Equal(RequisitosDeCastigo.NoEsAsociado, RequisitosDeCastigo.AportesCruzados, RequisitosDeCastigo.InformeMotivado,
            RequisitosDeCastigo.ConceptoDeIrrecuperabilidad, RequisitosDeCastigo.GestionesAgotadas);
        v.Problema!.Codigo.Should().Be("Lending.WriteOff.RequirementMissing");
        v.Problema.Datos!["missing"].Should().BeEquivalentTo(v.Faltantes);
        v.Requisitos.Single(r => r.Clave == RequisitosDeCastigo.NoEsAsociado).Evidencia.Should().Be("el deudor sigue siendo asociado");
    }

    [Fact]
    public void Sin_consulta_de_aportes_el_cruce_lo_acredita_un_soporte()
    {
        var sinConsulta = QueCumple() with { AportesCruzados = null };
        RequisitosDeCastigo.Verificar(sinConsulta, Politica).Faltantes.Should().Equal(RequisitosDeCastigo.AportesCruzados);

        var conSoporte = sinConsulta with { SoporteDeCruceDeAportes = Guid.Parse("33333333-3333-4333-8333-333333333333") };
        var v = RequisitosDeCastigo.Verificar(conSoporte, Politica);
        v.Cumple.Should().BeTrue();
        v.Requisitos.Single(r => r.Clave == RequisitosDeCastigo.AportesCruzados).Evidencia.Should().Contain("soporte");
    }

    [Fact]
    public void La_politica_deterioro_total_y_altura_minima()
    {
        var v = RequisitosDeCastigo.Verificar(QueCumple() with { DeterioroDelUltimoCierre = 1_200_000m, DiasDeMora = 200 }, Politica);

        v.Faltantes.Should().Equal(RequisitosDeCastigo.DeterioroTotal, RequisitosDeCastigo.AlturaMinima);
        v.Requisitos.Single(r => r.Clave == RequisitosDeCastigo.DeterioroTotal).Evidencia.Should().Contain("1200000").And.Contain("1500000");
        v.Requisitos.Single(r => r.Clave == RequisitosDeCastigo.AlturaMinima).Evidencia.Should().Contain("200").And.Contain("360");
    }

    [Fact]
    public void Lo_que_la_politica_no_exige_no_se_verifica()
    {
        var laxa = new PoliticaDeCastigo(ExigeDeterioroTotal: false, DiasMinimosDeMora: null, GestionesMinimas: 0);

        var v = RequisitosDeCastigo.Verificar(QueCumple() with { DeterioroDelUltimoCierre = 0m, DiasDeMora = 10 }, laxa);

        v.Cumple.Should().BeTrue();
        v.Requisitos.Select(r => r.Clave).Should().NotContain([RequisitosDeCastigo.DeterioroTotal, RequisitosDeCastigo.AlturaMinima]);
    }

    [Fact]
    public void Con_el_registro_de_gestiones_cuentan_las_gestiones()
    {
        var conGestiones = Politica with { GestionesMinimas = 3, GestionesRegistradas = true };

        RequisitosDeCastigo.Verificar(QueCumple() with { Gestiones = 2 }, conGestiones).Faltantes.Should().Equal(RequisitosDeCastigo.GestionesAgotadas);
        RequisitosDeCastigo.Verificar(QueCumple() with { Gestiones = 3 }, conGestiones).Cumple.Should().BeTrue();
    }

    [Fact]
    public void Los_requisitos_van_a_RequirementsJson_con_resultado_y_evidencia()
    {
        var v = RequisitosDeCastigo.Verificar(QueCumple() with { DeudorEsAsociado = true }, Politica);

        using var json = JsonDocument.Parse(v.RequisitosJson);
        var raiz = json.RootElement;
        raiz.GetProperty("loanId").GetInt32().Should().Be(7);
        raiz.GetProperty("missing").EnumerateArray().Select(x => x.GetString()).Should().Equal(RequisitosDeCastigo.NoEsAsociado);
        var primero = raiz.GetProperty("requirements")[0];
        primero.GetProperty("key").GetString().Should().Be(RequisitosDeCastigo.NoEsAsociado);
        primero.GetProperty("met").GetBoolean().Should().BeFalse();
        primero.GetProperty("evidence").GetString().Should().NotBeNullOrWhiteSpace();
        RequisitosDeCastigo.Verificar(QueCumple() with { DeudorEsAsociado = true }, Politica).RequisitosJson.Should().Be(v.RequisitosJson);
    }
}
