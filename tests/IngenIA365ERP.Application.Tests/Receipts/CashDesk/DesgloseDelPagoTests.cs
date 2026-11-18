using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>2026-11-17: el desglose que la caja muestra y el recibo imprime sale de la simulación del módulo, con el interés corriente y la mora aparte.</summary>
public class DesgloseDelPagoTests
{
    private static SaldosDeObligacionDto Saldos(decimal cancelar, decimal vencido = 0m, int mora = 0) =>
        new(cancelar, 0m, 0m, 0m, 0m, 0m, 0m, 0m, vencido, 0m, cancelar, mora);

    [Fact]
    public void Un_pago_a_credito_separa_los_rubros_junta_las_cuotas_y_dice_como_queda()
    {
        var s = new SimulacionDePagoDto("S1", Guid.NewGuid(), ModalidadDePago.PastDue, 120_000m, new DateOnly(2026, 11, 17), null, 120_000m, 5_000m, 0m, 0m,
        [
            new LineaDeDistribucionDto(1, ConceptoDeObligacion.Principal, 50_000m),
            new LineaDeDistribucionDto(1, ConceptoDeObligacion.CurrentInterest, 10_000m),
            new LineaDeDistribucionDto(1, ConceptoDeObligacion.DefaultInterest, 2_000m),
            new LineaDeDistribucionDto(2, ConceptoDeObligacion.Principal, 50_000m),
            new LineaDeDistribucionDto(2, ConceptoDeObligacion.Insurance, 3_000m),
            new LineaDeDistribucionDto(null, ConceptoDeObligacion.Advance, 5_000m),
        ],
        Saldos(400_000m), new PlanResumidoDto(10, 60_000m, new DateOnly(2026, 12, 15), null, []), [], []);

        var d = DesgloseDelPago.DeCredito(s);

        d.Concepts.Select(r => (r.Name, r.Amount)).Should().Equal(("Seguro", 3_000m), ("Interés de mora", 2_000m), ("Interés corriente", 10_000m), ("Capital", 100_000m));
        d.Installments.Should().Equal(1, 2);
        d.Advance.Should().Be(5_000m);
        d.PayoffAfter.Should().Be(400_000m);
        d.PaidOff.Should().BeFalse();
        d.NextDueDate.Should().Be(new DateOnly(2026, 12, 15));
        d.NextInstallment.Should().Be(60_000m);
    }

    [Fact]
    public void La_cancelacion_deja_el_credito_en_cero()
    {
        var s = new SimulacionDePagoDto("S2", Guid.NewGuid(), ModalidadDePago.FullPayoff, 80_000m, new DateOnly(2026, 11, 17), null, 80_000m, 0m, 0m, 0m,
            [new LineaDeDistribucionDto(3, ConceptoDeObligacion.Principal, 80_000m)], Saldos(0m), null, [], []);

        DesgloseDelPago.DeCredito(s).PaidOff.Should().BeTrue();
    }
}
