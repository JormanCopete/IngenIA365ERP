using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-13 (pedido del dueño): del crédito de una venta se piden sólo las cuotas y la fecha del primer pago. Sin plazo, la periodicidad es
/// la del medio y el plazo sale de las cuotas: el último vencimiento es el primero más (cuotas − 1) × periodicidad. Antes el plazo propuesto
/// del medio (30 días) no crecía con las cuotas. Con plazo explícito, todo como antes.
/// </summary>
public class CondicionesDelCreditoTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 13);

    /// <summary>Como CXCCOM de COOFLOPAL: 30 días propuestos, hasta 90, de 1 a 3 cuotas mensuales.</summary>
    private static PaymentMeans Medio() => new()
    {
        Code = "CXCCOM", Name = "Cuenta por cobrar comercial", DefaultTermDays = 30, MaxTermDays = 90, DefaultInstallments = 1,
        MaxInstallments = 3, InstallmentPeriodDays = 30,
    };

    [Fact]
    public void Una_cuota_sin_fecha_vence_al_plazo_propuesto_del_medio()
    {
        var c = CreditoEnLaVenta.Condiciones(Medio(), 1, null, null, null, Hoy);

        c.Should().Be(new CreditoEnLaVenta.CondicionesDeCredito(1, 30, 30, Hoy.AddDays(30), Hoy.AddDays(30)));
    }

    [Fact]
    public void Tres_cuotas_sin_fecha_vencen_cada_periodicidad_y_el_plazo_crece_con_ellas()
    {
        var c = CreditoEnLaVenta.Condiciones(Medio(), 3, null, null, null, Hoy);

        c.Should().Be(new CreditoEnLaVenta.CondicionesDeCredito(3, 90, 30, Hoy.AddDays(30), Hoy.AddDays(90)));
    }

    [Fact]
    public void Con_la_fecha_del_primer_pago_el_ultimo_vencimiento_y_el_plazo_salen_de_ella()
    {
        var c = CreditoEnLaVenta.Condiciones(Medio(), 2, null, null, new DateOnly(2026, 11, 30), Hoy);

        c.Should().Be(new CreditoEnLaVenta.CondicionesDeCredito(2, 47, 30, new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 30)));
    }

    [Theory]
    [InlineData(3, 31)] // el primer pago a 31 días deja la última cuota a 91: más que el máximo de 90
    [InlineData(4, 30)] // más cuotas que el máximo del medio
    [InlineData(1, 0)]  // el primer pago no puede ser hoy ni antes
    public void Lo_que_no_cabe_en_el_medio_no_tiene_condiciones(short cuotas, int diasAlPrimerPago)
    {
        CreditoEnLaVenta.Condiciones(Medio(), cuotas, null, null, Hoy.AddDays(diasAlPrimerPago), Hoy).Should().BeNull();
    }

    [Fact]
    public void Con_plazo_explicito_sigue_la_regla_de_antes()
    {
        var c = CreditoEnLaVenta.Condiciones(Medio(), 3, 90, 30, null, Hoy);

        c.Should().Be(new CreditoEnLaVenta.CondicionesDeCredito(3, 90, 30, Hoy.AddDays(30), Hoy.AddDays(90)));
    }

    [Fact]
    public void Aplicar_deja_en_el_pago_las_condiciones_completas_para_que_el_borrador_las_muestre()
    {
        var pago = new DocumentPayment();

        CreditoEnLaVenta.AplicarCondiciones(pago, Medio(), new PaymentCreditInput(3, null, null, FirstDueDate: new DateOnly(2026, 11, 20)), Hoy);

        (pago.InstallmentCount, pago.CreditTermDays, pago.InstallmentPeriodDays).Should().Be(((short)3, (short)67, (short)30));
        pago.FirstDueDate.Should().Be(new DateOnly(2026, 11, 20));
        pago.FinalDueDate.Should().Be(new DateOnly(2027, 1, 19));
    }

    [Fact]
    public void Aplicar_lo_que_no_cabe_lo_deja_como_vino_para_que_la_confirmacion_lo_rechace()
    {
        var pago = new DocumentPayment();

        CreditoEnLaVenta.AplicarCondiciones(pago, Medio(), new PaymentCreditInput(5, null, null), Hoy);

        pago.InstallmentCount.Should().Be(5);
        pago.CreditTermDays.Should().BeNull();
        pago.FinalDueDate.Should().BeNull();
    }
}
