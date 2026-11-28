using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Receipts.Paymasters;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Paymasters;

/// <summary>
/// Feature 028 (E2, T069, T073; R7, FR-022, HU4-5, D3): lo que la pagaduría gira por un crédito que ya recogió el desembolso de otro.
/// <list type="bullet">
/// <item>Cartera (contrato real): el pago a un crédito recogido se rechaza con <c>Lending.Payment.ObligationNotPayable</c> y en <c>data</c> el crédito nuevo y la
/// regla de <c>Recogida.GiroPosteriorDeCreditoRecogido</c> (de fábrica, saldo de la pagaduría); un crédito cancelado de otra forma recibe el rechazo de
/// siempre.</item>
/// <item>Recaudo (contrato simulado): con «SaldoDeLaPagaduria», saldo de la pagaduría con la alerta, como siempre; con «AlCreditoNuevo», la línea se aplica en
/// su recibo al crédito nuevo con valor libre, sin referencia de pagaduría y la clave <c>REC:{giro}:{línea}:sucesor</c>; con «AFavorDelAsociado», el saldo
/// pasa al asociado con su hecho; y lo que sobra del giro que canceló un crédito vivo sólo por la cuota dejada sigue la misma regla.</item>
/// </list>
/// (nuevo)
/// </summary>
public class GiroPosteriorDeCreditoRecogidoTests
{
    // ------------------------------------------------------------------------------------------------- Cartera: el rechazo --

    private static readonly DateOnly AMitad = PagosDePrueba.Hoy.AddDays(10);

    private static void Regla(PagosDePrueba p, string valor)
    {
        p.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.RecogidaGiroPosteriorDeCreditoRecogido, Value = valor,
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test", CreatedAt = p.G.UtcNow.AddDays(-10),
        });
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();
    }

    /// <summary>Recoge todo el crédito de Ana con un crédito nuevo de 5.000.000 desembolsado a mitad de la cuota 1; devuelve el crédito nuevo.</summary>
    private static async Task<VinculoDeRecogida> RecogidoAsync(PagosDePrueba p)
    {
        var s = await p.D.AprobadaAsync(p.D.O.Solicitud(monto: 5_000_000m) with
        {
            Payoffs = [new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Total)], PayoffProjection = new PayoffProjectionInput(AMitad),
        });
        await p.CausarHastaAsync(AMitad);
        await p.D.DesembolsadoAsync(p.D.Entrada(s) with { AcknowledgedPayoffDifferences = true }, p.Contrato());
        (await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.PaidOff);
        return (await VinculosDeRecogida.DeLosRecogidosAsync(p.Db, [p.Credito.Id], default)).Single();
    }

    [Fact]
    public async Task El_pago_a_un_credito_recogido_se_rechaza_con_el_credito_nuevo_y_la_regla_de_fabrica()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var vinculo = await RecogidoAsync(p);

        var simulada = await p.Contrato().SimularAsync(new SolicitudDeSimulacion(PagosDePrueba.Prueba, p.Credito.PublicId, ModalidadDePago.FreeAmount, 100_000m,
            AMitad), default);
        var aplicada = await p.AplicarAsync(p.Pago(ModalidadDePago.FreeAmount, 100_000m, AMitad));

        foreach (var (codigo, mensaje, datos) in new[] { (simulada.Code, simulada.Message, simulada.Data), (aplicada.Code, aplicada.Message, aplicada.Data) })
        {
            codigo.Should().Be(GiroPosteriorDeCreditoRecogido.CodigoNoPagable);
            mensaje.Should().Contain($"la recogió el crédito {vinculo.CreditoNuevoNumero}").And.Contain("saldo de la pagaduría");
            var sucesor = GiroPosteriorDeCreditoRecogido.Leer(codigo, datos);
            sucesor.Should().NotBeNull();
            (sucesor!.Obligacion, sucesor.Numero, sucesor.Desde, sucesor.Regla)
                .Should().Be((vinculo.CreditoNuevoPublicId, vinculo.CreditoNuevoNumero, (DateOnly?)vinculo.Fecha, GiroPosteriorDeCreditoRecogido.SaldoDeLaPagaduria));
        }
    }

    [Fact]
    public async Task Con_AlCreditoNuevo_el_rechazo_lo_dice_y_un_credito_cancelado_de_otra_forma_no_lleva_sucesor()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        Regla(p, ParametrosDeCartera.GiroPosteriorAlCreditoNuevo);
        var vinculo = await RecogidoAsync(p);

        var r = await p.AplicarAsync(p.Pago(ModalidadDePago.FreeAmount, 100_000m, AMitad));

        r.Message.Should().Contain($"se aplica al crédito {vinculo.CreditoNuevoNumero}");
        GiroPosteriorDeCreditoRecogido.Leer(r.Code, r.Data)!.AlCreditoNuevo.Should().BeTrue();

        // El crédito nuevo, cancelado en caja (no lo recogió nadie), recibe el rechazo de siempre.
        var nuevo = await p.Db.Loans.SingleAsync(l => l.Id == vinculo.CreditoNuevoId);
        nuevo.Status = LoanStatus.PaidOff;
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();
        var otro = await p.AplicarAsync(p.Pago(ModalidadDePago.FreeAmount, 100_000m, AMitad, obligacion: vinculo.CreditoNuevoPublicId));
        otro.Code.Should().Be(GiroPosteriorDeCreditoRecogido.CodigoNoPagable);
        GiroPosteriorDeCreditoRecogido.Leer(otro.Code, otro.Data).Should().BeNull();
    }

    [Fact]
    public void La_regla_se_lee_del_rechazo_como_venga()
    {
        var nuevo = Guid.NewGuid();
        var datos = GiroPosteriorDeCreditoRecogido.Datos("CR-1", "PaidOff", nuevo, "CR-2", new DateOnly(2026, 12, 11),
            GiroPosteriorDeCreditoRecogido.AFavorDelAsociado);
        var json = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(System.Text.Json.JsonSerializer.Serialize(datos))!;

        foreach (var d in new[] { datos, (IReadOnlyDictionary<string, object?>)json })
        {
            var s = GiroPosteriorDeCreditoRecogido.Leer(GiroPosteriorDeCreditoRecogido.CodigoNoPagable, d)!;
            (s.Obligacion, s.Numero, s.Desde, s.AFavorDelAsociado).Should().Be((nuevo, "CR-2", (DateOnly?)new DateOnly(2026, 12, 11), true));
        }
        GiroPosteriorDeCreditoRecogido.Leer("Lending.Payment.PaymasterReferenceInvalid", datos).Should().BeNull("otro rechazo no trae regla");
        GiroPosteriorDeCreditoRecogido.Leer(GiroPosteriorDeCreditoRecogido.CodigoNoPagable, null).Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------------- Recaudo: el giro --

    private readonly PagaduriasDePrueba _p = new();
    private readonly Guid _nuevo = Guid.NewGuid();

    /// <summary>El contrato rechaza el crédito A (ya recogido por <see cref="_nuevo"/>) con la regla; los demás, los aplica.</summary>
    private void RecogidoConRegla(string regla) =>
        _p.B.K.Obligaciones.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci => new RespuestaDeAplicacionDto(
            ci.Arg<SolicitudDeAplicacion>().Payments.Select(pago => pago.ObligationPublicId == _p.B.K.CreditoA
                ? new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(ResultadoDelContrato.Rejected, GiroPosteriorDeCreditoRecogido.CodigoNoPagable,
                    "La obligación CR-A ya no recibe pagos: la recogió el crédito CR-NUEVO.", null, Datos(regla))
                : CajaConContratos.Aplicado(pago)).ToList(), []));

    private IReadOnlyDictionary<string, object?> Datos(string regla) =>
        GiroPosteriorDeCreditoRecogido.Datos("CR-A", "PaidOff", _nuevo, "CR-NUEVO", EscenarioDeRecaudo.Hoy.AddDays(-10), regla);

    private async Task<(PaymasterRemittanceDto Giro, PayrollDeductionFile Archivo)> GiroAsync(decimal valor = 100_000m)
    {
        var f = await _p.RespondidoAsync(1, valor);
        var giro = await _p.RegistrarGiro().Handle(new RegisterPaymasterRemittanceCommand(_p.Pagaduria.PublicId, _p.B.Abono(valor).PublicId, [f.PublicId])
            { OperationKey = Guid.NewGuid() }, default);
        giro.IsSuccess.Should().BeTrue(giro.IsFailure ? giro.Error.Code : null);
        return (giro.Value, f);
    }

    private async Task<PaymasterRemittanceDto> AplicarAsync(Guid giro)
    {
        var r = await _p.AplicarGiro().Handle(new ApplyPaymasterRemittanceCommand(giro) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        return r.Value;
    }

    [Fact]
    public async Task Con_SaldoDeLaPagaduria_queda_como_siempre_saldo_de_la_pagaduria_con_la_alerta()
    {
        var (giro, _) = await GiroAsync();
        RecogidoConRegla(GiroPosteriorDeCreditoRecogido.SaldoDeLaPagaduria);

        var r = await AplicarAsync(giro.PublicId);

        r.Items.Single().Outcome.Should().Be(PaymasterRemittanceLineOutcome.Rejected);
        var saldo = await _p.B.K.C.E.Db.PayerCreditBalances.SingleAsync();
        (saldo.Kind, saldo.PersonId, saldo.Balance).Should().Be((PayerCreditBalanceKind.Paymaster, _p.Pagaduria.Id, 100_000m));
        await _p.B.K.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoNoAplicadoDePagaduria),
            Arg.Any<CancellationToken>());
        await _p.B.K.Obligaciones.DidNotReceive().AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.Any(x => x.ObligationPublicId == _nuevo)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_AlCreditoNuevo_la_linea_se_aplica_al_credito_que_lo_recogio_con_su_clave()
    {
        var (giro, f) = await GiroAsync();
        RecogidoConRegla(GiroPosteriorDeCreditoRecogido.AlCreditoNuevo);
        var linea = await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFileLines.SingleAsync(l => l.PayrollDeductionFileId == f.Id);

        var r = await AplicarAsync(giro.PublicId);

        var aplicada = r.Items.Single();
        aplicada.Outcome.Should().Be(PaymasterRemittanceLineOutcome.Applied, "la línea queda aplicada al crédito nuevo");
        aplicada.ReceiptNumber.Should().NotBeNull();
        r.Status.Should().Be(PaymasterRemittanceState.Applied);
        await _p.B.K.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.Count == 1
            && s.Payments[0].ObligationPublicId == _nuevo
            && s.Payments[0].Modality == ModalidadDePago.FreeAmount
            && s.Payments[0].MeansClass == ClaseDeMedioDeRecaudo.Transfer
            && s.Payments[0].DeductionFileLinePublicId == null
            && s.Payments[0].Amount == 100_000m
            && s.Payments[0].ValueDate == linea.DeductionDate
            && s.Payments[0].IdempotencyKey == GirosDePagaduria.ClaveDelSucesor(giro.PublicId, linea.DeductionFileLinePublicId)), Arg.Any<CancellationToken>());
        (await _p.B.K.C.E.Db.PayerCreditBalances.AnyAsync()).Should().BeFalse("no queda saldo de la pagaduría");
        (await _p.B.K.C.E.Db.IssuedReceipts.SingleAsync()).Channel.Should().Be(ReceiptChannel.Paymaster);
        await _p.B.K.C.E.Alertas.DidNotReceive().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoNoAplicadoDePagaduria),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_AFavorDelAsociado_el_saldo_pasa_a_la_persona_con_su_hecho_y_sin_alerta()
    {
        var (giro, _) = await GiroAsync();
        RecogidoConRegla(GiroPosteriorDeCreditoRecogido.AFavorDelAsociado);

        var r = await AplicarAsync(giro.PublicId);

        r.Items.Single().Outcome.Should().Be(PaymasterRemittanceLineOutcome.Rejected);
        var saldos = await _p.B.K.C.E.Db.PayerCreditBalances.AsNoTracking().ToListAsync();
        var deLaPagaduria = saldos.Single(s => s.Kind == PayerCreditBalanceKind.Paymaster);
        var delAsociado = saldos.Single(s => s.Kind == PayerCreditBalanceKind.Payer);
        (deLaPagaduria.Balance, deLaPagaduria.Status).Should().Be((0m, PayerCreditBalanceState.Resolved));
        (delAsociado.PersonId, delAsociado.Balance, delAsociado.Status).Should().Be((_p.B.K.Asociado.Id, 100_000m, PayerCreditBalanceState.Open));
        (await _p.B.K.C.E.Db.PayerCreditBalanceMovements.SingleAsync(m => m.PayerCreditBalanceId == deLaPagaduria.Id
            && m.Kind == PayerCreditBalanceMovementKind.TransferredToAssociate)).Amount.Should().Be(100_000m);
        (await _p.B.K.C.E.Db.PaymasterRemittanceLines.SingleAsync()).CreditBalanceId.Should().Be(delAsociado.Id);
        (await _p.B.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == SaldoAFavorDePagadorResueltoV1.Type)).Should().Be(1);
        await _p.B.K.C.E.Alertas.DidNotReceive().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoNoAplicadoDePagaduria),
            Arg.Any<CancellationToken>());

        // Lo que pasó al asociado no se reprocesa desde el giro: se resuelve en «Saldos a favor».
        var linea = await _p.B.K.C.E.Db.PaymasterRemittanceLines.SingleAsync();
        var corrida = await _p.B.K.C.E.Db.ProcessingRuns.FirstAsync();
        var deCorrida = await _p.B.K.C.E.Db.ProcessingRunLines.FirstAsync(x => x.IdempotencyKey == linea.IdempotencyKey);
        (await _p.Giros.ReprocesarAsync(corrida, deCorrida, _p.B.K.C.E.Supervisor.Id, default)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Lo_que_sobra_del_giro_que_cancela_la_cuota_dejada_va_al_credito_nuevo()
    {
        var (giro, _) = await GiroAsync();
        // El crédito A vivía sólo por la cuota dejada: el giro la cubre con 70.000 y sobran 30.000; después, Cartera lo dice recogido.
        _p.B.K.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeSimulacion>();
            if (s.ObligationPublicId == _p.B.K.CreditoA && s.Modality == ModalidadDePago.FreeAmount)
                return new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Rejected, GiroPosteriorDeCreditoRecogido.CodigoNoPagable, "Recogido.",
                    null, Datos(GiroPosteriorDeCreditoRecogido.AlCreditoNuevo));
            var sim = CajaConContratos.Simulacion(s, null, []);
            return new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "OK", "",
                s.ObligationPublicId == _p.B.K.CreditoA ? sim with { AppliedAmount = s.Amount - 30_000m, Surplus = 30_000m } : sim);
        });

        var r = await AplicarAsync(giro.PublicId);

        r.Items.Single().Outcome.Should().Be(PaymasterRemittanceLineOutcome.Applied);
        await _p.B.K.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.Count == 1 && s.Payments[0].ObligationPublicId == _nuevo
            && s.Payments[0].Amount == 30_000m && s.Payments[0].IdempotencyKey.EndsWith(":sucesor")), Arg.Any<CancellationToken>());
        var sobrante = await _p.B.K.C.E.Db.PayerCreditBalances.AsNoTracking().SingleAsync();
        (sobrante.Kind, sobrante.Balance, sobrante.Status).Should().Be((PayerCreditBalanceKind.Paymaster, 0m, PayerCreditBalanceState.Resolved),
            "lo que sobró lo tomó el recibo del crédito nuevo");
        (await _p.B.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Regresion_lo_que_sobra_de_un_credito_que_nadie_recogio_queda_como_saldo_de_la_pagaduria()
    {
        var (giro, _) = await GiroAsync();
        _p.B.K.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeSimulacion>();
            var sim = CajaConContratos.Simulacion(s, null, []);
            return new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "OK", "",
                s.Modality == ModalidadDePago.FreeAmount ? sim : sim with { AppliedAmount = s.Amount - 30_000m, Surplus = 30_000m });
        });

        await AplicarAsync(giro.PublicId);

        var sobrante = await _p.B.K.C.E.Db.PayerCreditBalances.AsNoTracking().SingleAsync();
        (sobrante.Kind, sobrante.Balance).Should().Be((PayerCreditBalanceKind.Paymaster, 30_000m));
        (await _p.B.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(1);
    }
}
