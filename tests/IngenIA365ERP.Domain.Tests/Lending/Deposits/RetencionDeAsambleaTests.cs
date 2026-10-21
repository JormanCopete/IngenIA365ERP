using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Respuesta 89 B del dueño (E260): la retención en la fuente sobre la revalorización de aportes y los retornos cooperativos. El cálculo es puro y no trae
/// ningún valor legal: la tarifa, el concepto y la base mínima los fija cada cooperativa. (nuevo)
/// </summary>
public class RetencionDeAsambleaTests
{
    [Fact]
    public void Retiene_la_tarifa_sobre_lo_que_recibe_el_asociado_al_peso()
    {
        var r = RetencionDeAsamblea.Calcular(33_333m, 0.035m, null, PerfilDeRetencion.Standard, 1m);
        r.SeRetiene.Should().BeTrue();
        r.Valor.Should().Be(1_167m, "33.333 × 3,5 % = 1.166,655 → al peso, mitad hacia arriba");
        r.Base.Should().Be(33_333m);
        r.MotivoDeExencion.Should().BeNull();
    }

    [Fact]
    public void Al_centavo_y_sin_pasar_de_la_base()
    {
        RetencionDeAsamblea.Calcular(33_333m, 0.035m, null, PerfilDeRetencion.Standard, 0.01m).Valor.Should().Be(1_166.66m, "al centavo, mitad hacia arriba");
        RetencionDeAsamblea.Calcular(100m, 1m, null, PerfilDeRetencion.Standard, 1m).Valor.Should().Be(100m, "la tarifa del 100 % retiene todo y no más");
    }

    [Theory]
    [InlineData(PerfilDeRetencion.NonTaxpayer, "NonTaxpayer")]
    [InlineData(PerfilDeRetencion.SelfWithholder, "SelfWithholder")]
    public void No_retiene_a_quien_no_es_retenible(PerfilDeRetencion perfil, string motivo)
    {
        var r = RetencionDeAsamblea.Calcular(100_000m, 0.1m, null, perfil, 1m);
        r.SeRetiene.Should().BeFalse();
        r.MotivoDeExencion.Should().Be(motivo);
    }

    [Fact]
    public void Sin_perfil_registrado_se_retiene_como_a_un_retenible()
    {
        RetencionDeAsamblea.Calcular(100_000m, 0.1m, null, PerfilDeRetencion.NoProfile, 1m).Valor.Should().Be(10_000m);
    }

    [Fact]
    public void Bajo_la_base_minima_no_se_retiene_y_desde_ella_si()
    {
        var bajo = RetencionDeAsamblea.Calcular(39_999.99m, 0.1m, 40_000m, PerfilDeRetencion.Standard, 1m);
        bajo.SeRetiene.Should().BeFalse();
        bajo.MotivoDeExencion.Should().Be("BelowThreshold");
        bajo.UmbralEnPesos.Should().Be(40_000m);
        RetencionDeAsamblea.Calcular(40_000m, 0.1m, 40_000m, PerfilDeRetencion.Standard, 1m).Valor.Should().Be(4_000m, "la base mínima es inclusiva");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public void Sin_tarifa_no_se_retiene(double? tarifa)
    {
        var r = RetencionDeAsamblea.Calcular(100_000m, tarifa is null ? null : (decimal)tarifa, null, PerfilDeRetencion.Standard, 1m);
        r.SeRetiene.Should().BeFalse();
        r.MotivoDeExencion.Should().Be("ZeroRate");
    }

    [Fact]
    public void La_politica_sin_concepto_no_tiene_tarifa_aunque_se_haya_escrito()
    {
        var sinConcepto = new PoliticaDeRetencionDeAsamblea(" ", 0.1m, 0.2m, null);
        sinConcepto.Configurada.Should().BeFalse();
        sinConcepto.TarifaDe(AssemblyDistributionKind.Revaluation).Should().BeNull();
        sinConcepto.TarifaDe(AssemblyDistributionKind.SurplusDistribution).Should().BeNull();

        var configurada = new PoliticaDeRetencionDeAsamblea("RETEFTE-ASAMBLEA", 0.1m, 0.2m, 5m);
        configurada.TarifaDe(AssemblyDistributionKind.Revaluation).Should().Be(0.1m);
        configurada.TarifaDe(AssemblyDistributionKind.SurplusDistribution).Should().Be(0.2m);
        configurada.TarifaDe(AssemblyDistributionKind.Amortization).Should().BeNull("devolver aportes no es renta del asociado");
    }

    [Fact]
    public void Un_valor_negativo_es_un_error_de_quien_llama()
    {
        var mal = () => RetencionDeAsamblea.Calcular(-1m, 0.1m, null, PerfilDeRetencion.Standard, 1m);
        mal.Should().Throw<ArgumentOutOfRangeException>();
    }
}
