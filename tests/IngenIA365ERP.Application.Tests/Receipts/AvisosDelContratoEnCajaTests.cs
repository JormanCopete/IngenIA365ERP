using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Lending.Payments;

namespace IngenIA365ERP.Application.Tests.Receipts;

/// <summary>
/// 2026-11-24 (pedido del dueño): la Caja mostraba los avisos del pago como códigos («Lending.Payment.AdvanceDoesNotReduceInterest»,
/// «Lending.Payment.TreatedAsPayoff»…). Los textos viven en el contrato de obligaciones, que Recaudo puede usar, y son los mismos de la simulación de
/// Cartera. Los códigos del contrato son los del aplicador de pagos: si uno cambia, esta prueba lo dice.
/// </summary>
public class AvisosDelContratoEnCajaTests
{
    [Fact]
    public void Los_codigos_del_contrato_son_los_del_aplicador()
    {
        AvisosDelContratoDeObligaciones.AnticipoNoReduceIntereses.Should().Be(AplicadorDePagos.AvisoAnticipoNoReduceIntereses);
        AvisosDelContratoDeObligaciones.PreferenciaAsumida.Should().Be(AplicadorDePagos.AvisoPreferenciaAsumida);
        AvisosDelContratoDeObligaciones.TratadoComoCancelacion.Should().Be(AplicadorDePagos.AvisoTratadoComoCancelacion);
        AvisosDelContratoDeObligaciones.InteresDeLaCuotaCompleta.Should().Be(AplicadorDePagos.AvisoInteresDeLaCuotaCompleta);
    }

    [Theory]
    [InlineData(AplicadorDePagos.AvisoAnticipoNoReduceIntereses)]
    [InlineData(AplicadorDePagos.AvisoPreferenciaAsumida)]
    [InlineData(AplicadorDePagos.AvisoTratadoComoCancelacion)]
    [InlineData(AplicadorDePagos.AvisoInteresDeLaCuotaCompleta)]
    public void Cada_aviso_conocido_se_dice_en_espanol_y_no_muestra_el_codigo(string codigo)
    {
        var texto = AvisosDelContratoDeObligaciones.Texto(codigo);

        texto.Should().NotContain("Lending.").And.NotBeNullOrWhiteSpace();
        SimulateLoanPaymentQueryHandler.Mensaje(codigo).Should().Be(texto, "Cartera y Recaudo dicen lo mismo");
    }

    [Fact]
    public void Un_codigo_desconocido_se_muestra_tal_cual()
    {
        AvisosDelContratoDeObligaciones.Textos(["Lending.Algo.Nuevo", AplicadorDePagos.AvisoTratadoComoCancelacion])
            .Should().HaveCount(2).And.Contain("Lending.Algo.Nuevo");
    }
}
