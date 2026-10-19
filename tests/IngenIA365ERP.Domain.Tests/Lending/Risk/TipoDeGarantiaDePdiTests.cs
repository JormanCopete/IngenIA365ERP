using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, K6a, bloque 2 (T1036): <see cref="TipoDeGarantiaDePdi"/>. Cada clase de garantía va a su tipo de la tabla de PDI del Anexo 2 (la idónea
/// sólo perfeccionada); el codeudor, el avalista y los aportes son «no idónea»; el pagaré y la falta de garantías, «sin garantía»; con varias, la PDI
/// más conservadora en el estado del crédito (pendiente de validar por la contadora). Los valores salen del Anexo 2 sembrado.
/// </summary>
public class TipoDeGarantiaDePdiTests
{
    private static Anexo2 Semilla() =>
        Anexo2.Leer(IngenIA365ERP.Persistence.Seeding.Parametric.PlatformNationalValuesSeeder.Contenido("sarc-anexo-2.json"), 1).Documento!;

    [Theory]
    [InlineData(CollateralKind.Pledge, true, "PRENDARIA")]
    [InlineData(CollateralKind.Pledge, false, "NO_IDONEA")]
    [InlineData(CollateralKind.Mortgage, true, "HIPOTECARIA")]
    [InlineData(CollateralKind.GuaranteeFund, true, "FONDOS_GARANTIAS")]
    [InlineData(CollateralKind.CoDebtor, true, "NO_IDONEA")]
    // Feature 014 (D39): los aportes no entran a la PDI (Anexo 2, num. 4.3); se restan del VEA.
    [InlineData(CollateralKind.Contributions, true, "SIN_GARANTIA")]
    [InlineData(CollateralKind.PromissoryNote, true, "SIN_GARANTIA")]
    public void Cada_clase_va_a_su_tipo(CollateralKind clase, bool perfeccionada, string tipo) =>
        TipoDeGarantiaDePdi.De(new GarantiaParaPdi(clase, perfeccionada)).Should().Be(tipo);

    [Fact]
    public void Sin_garantias_es_sin_garantia_y_un_codeudor_suma_no_idonea()
    {
        TipoDeGarantiaDePdi.Candidatos([], tieneCodeudores: false).Should().Equal(TipoDeGarantiaDePdi.SinGarantia);
        TipoDeGarantiaDePdi.Candidatos([new GarantiaParaPdi(CollateralKind.Mortgage, true)], tieneCodeudores: true)
            .Should().Equal(TipoDeGarantiaDePdi.Hipotecaria, TipoDeGarantiaDePdi.NoIdonea);
    }

    [Fact]
    public void Con_varias_toma_la_PDI_mas_conservadora_en_el_estado_del_credito()
    {
        var anexo = Semilla();
        var candidatos = new[] { TipoDeGarantiaDePdi.Hipotecaria, TipoDeGarantiaDePdi.NoIdonea };
        var sinIncumplir = TipoDeGarantiaDePdi.ElMasConservador(anexo, "CONSUMO_SIN_LIBRANZA", candidatos, incumplido: false, diasEnIncumplimiento: 0);
        var pdiDe = (string t) => anexo.Segmento("CONSUMO_SIN_LIBRANZA")!.PdiSinIncumplimiento.FirstOrDefault(x => x.TipoDeGarantia == t)?.Fraccion
                                  ?? anexo.Pdi(t)!.Tramos[0].Fraccion;

        pdiDe(sinIncumplir).Should().Be(candidatos.Max(pdiDe));
        var incumplido = TipoDeGarantiaDePdi.ElMasConservador(anexo, "CONSUMO_SIN_LIBRANZA", candidatos, incumplido: true, diasEnIncumplimiento: 400);
        anexo.Pdi(incumplido)!.EnIncumplimiento(400).Should().Be(candidatos.Max(t => anexo.Pdi(t)!.EnIncumplimiento(400)));
    }
}
