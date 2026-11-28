using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 028, T062a (D13 ajustada por el dueño el 2026-11-27; Ley 1555 de 2012): dentro del tope legal del prepago —o sin los valores nacionales para
/// saberlo— la cancelación salvo la cuota causada deja la cuota con el <b>interés proporcional</b>: lo causado hasta la fecha valor sobre todo el saldo y, de ahí
/// a su vencimiento, sólo sobre su propio capital. Por el contrato real (<see cref="PagosDePrueba"/>: Ana, 3.000.000 a 24 cuotas desde el 01-12-2026, la cuota 1
/// vence el 01-01-2027; SMMLV 1.750.905 × 880 = el tope, el crédito queda dentro): la recogida y la caja (Recaudo) dejan la cuota con la misma regla, la decisión
/// la sella con su motivo y la reconstrucción la repite; lo que se cancela no cambia; su canal la cierra al peso y, si paga lo que se le envió, lo de más es
/// excedente. (nuevo)
/// </summary>
public class CuotaDejadaProporcionalEnElContratoTests
{
    private static readonly DateOnly AMitad = PagosDePrueba.Hoy.AddDays(10);
    private static readonly DateOnly VenceLaPrimera = new(2027, 1, 1);
    private static readonly Recaudador Recogida = new("CAR", ClaseDeRecaudador.OwnLoanPayoff);

    private static PagoAAplicar DeLaRecogida(PagosDePrueba p, decimal valor) => p.Pago(ModalidadDePago.PayoffExceptCurrentInstallment, valor, AMitad,
        clave: $"CAR:{Guid.NewGuid():N}:{p.Credito.PublicId:N}", medio: ClaseDeMedioDeRecaudo.InternalOffset);

    private static async Task<Domain.Lending.Replay.EstadoDelCredito> EstadoAsync(PagosDePrueba p, DateOnly fecha) =>
        MotorDeCartera.Reconstruir((await p.C.Cargador().CargarAsync(p.Credito.Id, null, fecha, default))!.Entrada!, fecha);

    [Fact]
    public async Task Dentro_del_tope_la_recogida_deja_la_cuota_con_el_proporcional_y_lo_sella()
    {
        using var p = await PagosDePrueba.CrearAsync();
        await p.CausarHastaAsync(AMitad);
        var antes = await EstadoAsync(p, AMitad);
        var cuota1 = antes.Plan!.Cuotas[0];
        var completo = antes.InteresPorCausarDeLaCuotaEnCurso().Sum(l => l.Monto);
        var proporcional = antes.InteresPorCausarSobreSuCapital(1).Sum(l => l.Monto);
        proporcional.Should().BePositive().And.BeLessThan(completo, "sólo sobre el capital de la cuota 1, no sobre los 3.000.000");
        var tal = AplicadorDePagos.ValorParaCancelarSalvoCuota(antes, null, cuotaCompleta: false);
        var esperado = AplicadorDePagos.ValorParaCancelarSalvoCuota(antes, null, cuotaCompleta: false, proporcional: true);
        esperado.ParaCancelar.Should().Be(tal.ParaCancelar, "lo que se cancela no cambia");
        esperado.QuedaTalComoSeEnvio.Should().Be(tal.QuedaEnLaCuota);

        // La regla legal sale de un solo sitio: dentro del tope, el proporcional.
        var regla = await p.Intereses().CuotaDejadaAsync(p.Credito, antes.RulesVersion, default);
        (regla.Regla, regla.Motivo).Should().Be((AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional, InteresesAlCancelar.MotivoDentroDelTope));

        var simulada = await p.Contrato().SimularAsync(new SolicitudDeSimulacion(Recogida, p.Credito.PublicId,
            ModalidadDePago.PayoffExceptCurrentInstallment, 0m, AMitad), default);
        simulada.Result.Should().Be(ResultadoDelContrato.Answered, simulada.Message);
        simulada.Value!.AppliedAmount.Should().Be(esperado.ParaCancelar);
        simulada.Value.Notices.Should().Contain(AplicadorDePagos.AvisoCuotaCausadaDejada).And.Contain(AplicadorDePagos.AvisoCuotaDejadaProporcional);

        var pago = DeLaRecogida(p, esperado.ParaCancelar);
        var r = await p.AplicarAsync(pago, recaudador: Recogida);
        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        (r.Value!.PaidOff, r.Value.Surplus).Should().Be((false, 0m));
        r.Value.Warnings.Should().Contain(AplicadorDePagos.AvisoCuotaDejadaProporcional);
        var repetido = await p.AplicarAsync(pago, recaudador: Recogida);
        repetido.Value!.Warnings.Should().Contain(AplicadorDePagos.AvisoCuotaDejadaProporcional, "el comprobante repetido lo dice igual");

        var op = await p.OperacionAsync(r.Value.OperationPublicId);
        var decision = DecisionDePago.Leer(op.Payload)!;
        (decision.Installment, decision.LeftInstallmentRule, decision.LeftInstallmentRuleReason, decision.LeftInstallmentInterest)
            .Should().Be(((int?)1, AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional, InteresesAlCancelar.MotivoDentroDelTope, (decimal?)proporcional));
        decision.Contenido().CuotaDejadaProporcional.Should().BeTrue("la reconstrucción repite la regla sellada");
        var causada = (await p.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id).ToListAsync())
            .Should().ContainSingle(t => t.Kind == LoanTransactionKind.UnaccruedInstallmentInterest).Subject;
        (causada.InstallmentNumber, causada.Amount).Should().Be((1, proporcional));

        var despues = await EstadoAsync(p, AMitad.AddDays(5));
        despues.Saldo(LoanConcept.CurrentInterest, 1).Should().Be(cuota1.Interes - (completo - proporcional),
            "la cuota 1 lleva lo causado sobre todo el saldo y el resto del período sólo sobre su capital; congelada, no causa más");
        despues.ValorParaCancelar.Should().Be(esperado.QuedaEnLaCuota);
        await p.ElLibroCuadraAsync();
    }

    /// <summary>
    /// Su canal la paga al vencer: por lo que quedó en ella el crédito queda cancelado al peso; por lo que se le envió (tal como se envió), lo de más es
    /// excedente —no entra a Cartera: lo trata <c>Recogida.GiroPosteriorDeCreditoRecogido</c> si es la pagaduría (H2)—.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Su_canal_la_cierra_al_peso_y_lo_enviado_de_mas_es_excedente(bool pagaLoEnviado)
    {
        using var p = await PagosDePrueba.CrearAsync();
        await p.CausarHastaAsync(AMitad);
        var esperado = AplicadorDePagos.ValorParaCancelarSalvoCuota(await EstadoAsync(p, AMitad), null, cuotaCompleta: false, proporcional: true);
        (await p.AplicarAsync(DeLaRecogida(p, esperado.ParaCancelar), recaudador: Recogida)).Result.Should().Be(ResultadoDelContrato.Applied);

        await p.CausarHastaAsync(VenceLaPrimera);
        var valor = pagaLoEnviado ? esperado.QuedaTalComoSeEnvio : esperado.QuedaEnLaCuota;
        var cuota = await p.AplicadoAsync(p.Pago(ModalidadDePago.PastDue, valor, VenceLaPrimera));

        (cuota.PaidOff, cuota.Surplus).Should().Be((true, pagaLoEnviado ? esperado.DeMasSiSePagaComoSeEnvio : 0m));
        esperado.DeMasSiSePagaComoSeEnvio.Should().BePositive();
        (await p.Db.Loans.AsNoTracking().SingleAsync(x => x.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.PaidOff);
        await p.ElLibroCuadraAsync();
    }

    /// <summary>
    /// La caja (Recaudo, modalidad 8) y el contrato dicen lo mismo: lo que el listado ofrece (<c>ValorParaCancelarSalvoCuotaAsync</c>) es lo que el pago de la
    /// caja deja en la cuota, con la misma regla sellada. (Sin los valores nacionales la caja no cancela —sella el tope del prepago, como siempre—; el motivo
    /// <c>LegalPrepaymentCapUnknown</c> lo usa la recogida, que no lo sella: lo prueban el listado y el desembolso.)
    /// </summary>
    [Fact]
    public async Task La_caja_deja_la_cuota_con_la_misma_regla_que_ofrece_el_listado()
    {
        const string motivo = InteresesAlCancelar.MotivoDentroDelTope;
        using var p = await PagosDePrueba.CrearAsync();
        p.Recaudadores[ClavesDeRecaudo.Codigo] = IntegrationParticipantClass.Receipts;
        await p.CausarHastaAsync(AMitad);
        var ofrecido = (await p.Aplicacion().ValorParaCancelarSalvoCuotaAsync(p.Credito, await EstadoAsync(p, AMitad), default))!;
        ofrecido.Proporcional.Should().BeTrue();

        var r = await p.AplicarAsync(p.Pago(ModalidadDePago.PayoffExceptCurrentInstallment, ofrecido.ParaCancelar, AMitad,
            clave: ClavesDeRecaudo.Porcion(Guid.NewGuid(), Guid.NewGuid(), PaymentMeansClass.Cash)), recaudador: ClavesDeRecaudo.Recaudador);

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        var decision = DecisionDePago.Leer((await p.OperacionAsync(r.Value!.OperationPublicId)).Payload)!;
        (decision.LeftInstallmentRule, decision.LeftInstallmentRuleReason).Should().Be((AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional, motivo));
        (await EstadoAsync(p, AMitad.AddDays(3))).ValorParaCancelar.Should().Be(ofrecido.QuedaEnLaCuota, "lo que la caja ofreció dejar es lo que quedó");
    }
}
