using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Lotes;

namespace IngenIA365ERP.Domain.Tests.Treasury;

/// <summary>
/// Feature 015, D-a (decisión del dueño del 2026-10-27; FR-229): Tesorería resuelve ella misma una transferencia que llegó sin cuenta destino —completándola o
/// girándola por cheque— sólo mientras la validación puede mover la orden, sin archivo de Cartera ni anulación pedida, y sólo si le falta la cuenta destino.
/// </summary>
public class ResolucionDeDestinoTests
{
    private static readonly string[] SinDestino = [ResolucionDeDestino.MotivoSinCuentaDestino];

    [Theory]
    [InlineData(PaymentOrderState.Received)]
    [InlineData(PaymentOrderState.Pending)]
    [InlineData(PaymentOrderState.Ready)]
    public void Una_transferencia_sin_cuenta_destino_se_resuelve(PaymentOrderState estado) =>
        ResolucionDeDestino.Evaluar(estado, PaymentForm.Transfer, false, false, SinDestino).Should().BeNull();

    [Fact]
    public void Un_pago_a_tercero_sin_cuenta_destino_tambien() =>
        ResolucionDeDestino.Evaluar(PaymentOrderState.Pending, PaymentForm.ThirdPartyPayment, false, false, SinDestino).Should().BeNull();

    [Theory]
    [InlineData(PaymentOrderState.InBatch)]
    [InlineData(PaymentOrderState.Approved)]
    [InlineData(PaymentOrderState.Sent)]
    [InlineData(PaymentOrderState.Disbursed)]
    [InlineData(PaymentOrderState.Voided)]
    [InlineData(PaymentOrderState.RejectedOnArrival)]
    public void En_lote_enviada_o_cerrada_no_se_resuelve(PaymentOrderState estado) =>
        ResolucionDeDestino.Evaluar(estado, PaymentForm.Transfer, false, false, SinDestino).Should().Be(ResolucionDeDestino.NoResoluble);

    [Fact]
    public void La_pagada_por_archivo_de_Cartera_o_con_anulacion_pedida_no_se_resuelve()
    {
        ResolucionDeDestino.Evaluar(PaymentOrderState.Pending, PaymentForm.Transfer, true, false, SinDestino).Should().Be(ResolucionDeDestino.NoResoluble);
        ResolucionDeDestino.Evaluar(PaymentOrderState.Pending, PaymentForm.Transfer, false, true, SinDestino).Should().Be(ResolucionDeDestino.NoResoluble);
    }

    [Fact]
    public void Si_no_le_falta_la_cuenta_o_ya_es_cheque_no_hay_nada_que_resolver()
    {
        ResolucionDeDestino.Evaluar(PaymentOrderState.Pending, PaymentForm.Transfer, false, false, ["Treasury.Order.DestinationNotRegistered"])
            .Should().Be(ResolucionDeDestino.NoLeFaltaDestino);
        ResolucionDeDestino.Evaluar(PaymentOrderState.Pending, PaymentForm.Check, false, false, SinDestino).Should().Be(ResolucionDeDestino.NoLeFaltaDestino);
    }
}
