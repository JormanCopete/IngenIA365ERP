using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 013, US5, T0519 (interfaces §3.9; data-model §7.10; FR-076, FR-083, D7, D24): resolver un saldo a favor o un anticipo. Devolverlo por el
/// recaudador lo baja y emite <c>SaldoAFavorResuelto</c>; aplicarlo a otra obligación es un pago <c>InternalOffset</c> del deudor con la clave
/// <c>{clave}:destino</c>, sólo a obligaciones del mismo deudor; aplicarlo a capital sólo vale para un saldo nacido de un abono sin preferencia, con la
/// elección del deudor y la versión nueva del plan; más de lo disponible es <c>Lending.CreditBalance.Insufficient</c>; un saldo en una obligación
/// cancelada se resuelve aquí.
/// </summary>
public class ResolverSaldoAFavorTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Un abono sin preferencia (revocada): queda como saldo a favor.</summary>
    private async Task<decimal> SaldoAFavorAsync(decimal valor = 500_000m)
    {
        _p.G.UtcNow = _p.G.UtcNow.AddMinutes(5);
        _p.Db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(_p.D.O.Ana.Id, _p.Credito.Id, null, null, 2, _p.G.UtcNow.AddMinutes(-1),
            PreferenceOrigin.DebtorRequest, null));
        await _p.Db.SaveChangesAsync();
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.ExtraordinaryPrincipal, valor));
        c.CreditBalanceAmount.Should().Be(valor);
        return valor;
    }

    private Task<RespuestaDelContratoDto<ComprobanteDeResolucionDto>> ResolverAsync(FormaDeResolucion forma, decimal monto,
        ConceptoDeObligacion concepto = ConceptoDeObligacion.CreditBalance, Guid? destino = null, ModalidadDePago? modalidad = null, EleccionDeAbono? eleccion = null,
        string? clave = null) =>
        _p.Contrato().ResolverSaldoAFavorAsync(new SolicitudDeResolucion(PagosDePrueba.Prueba, clave ?? PagosDePrueba.Clave(), _p.Credito.PublicId, concepto, monto, forma,
            new ReferenciaDeRecibo("EG-TST-0001", Hoy, null, null, "Egreso"), destino, modalidad, eleccion), default);

    private async Task<decimal> SaldoListadoAsync(Guid? obligacion = null, ConceptoDeObligacion concepto = ConceptoDeObligacion.CreditBalance)
    {
        _p.Db.ChangeTracker.Clear();
        var l = await _p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, _p.D.O.Ana.PublicId, _p.G.HoyLocal), default);
        var o = l.Value!.Obligations.Single(x => x.ObligationPublicId == (obligacion ?? _p.Credito.PublicId));
        return concepto == ConceptoDeObligacion.Advance ? o.Advance : o.CreditBalance;
    }

    [Fact]
    public async Task Devolverlo_por_el_recaudador_baja_el_saldo_a_favor_y_emite_SaldoAFavorResuelto()
    {
        await SaldoAFavorAsync();

        var r = await ResolverAsync(FormaDeResolucion.RefundThroughCollector, 300_000m);

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        r.Code.Should().Be("Lending.ObligationContract.CreditBalanceResolved");
        r.Value!.BalancesAfter.CreditBalance.Should().Be(200_000m);
        (await SaldoListadoAsync()).Should().Be(200_000m);
        var op = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == r.Value.OperationPublicId);
        op.Kind.Should().Be(LoanOperationKind.CreditBalanceResolution);
        (await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id).ToListAsync())
            .Should().ContainSingle().Which.Should().Match<Domain.Entities.Lending.Transactions.LoanTransaction>(t =>
                t.Kind == LoanTransactionKind.CreditBalanceSettled && t.Concept == LoanConcept.CreditBalance && t.Amount == -300_000m);
        var hecho = await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == SaldoAFavorResueltoV1.Type);
        hecho.OriginPublicId.Should().Be(op.PublicId);
        hecho.PayloadJson.Should().Contain("\"resolution\":\"RefundThroughCollector\"").And.Contain("\"balanceKind\":\"CreditBalance\"");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Un_anticipo_tambien_se_devuelve()
    {
        await _p.AplicadoAsync(_p.Pago(ModalidadDePago.Advance, 150_000m));

        var r = await ResolverAsync(FormaDeResolucion.RefundThroughCollector, 150_000m, ConceptoDeObligacion.Advance);

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        (await SaldoListadoAsync(concepto: ConceptoDeObligacion.Advance)).Should().Be(0m);
        (await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == SaldoAFavorResueltoV1.Type)).PayloadJson
            .Should().Contain("\"balanceKind\":\"Advance\"");
    }

    [Fact]
    public async Task Mas_de_lo_disponible_responde_Insufficient()
    {
        await SaldoAFavorAsync(100_000m);
        var r = await ResolverAsync(FormaDeResolucion.RefundThroughCollector, 100_000.01m);
        r.Code.Should().Be("Lending.CreditBalance.Insufficient");
        r.Data!["available"].Should().Be(100_000m);
    }

    [Fact]
    public async Task Aplicarlo_a_capital_usa_la_eleccion_del_deudor_y_regenera_el_plan()
    {
        await SaldoAFavorAsync();

        var r = await ResolverAsync(FormaDeResolucion.ApplyAsExtraordinaryPrincipal, 500_000m, eleccion: EleccionDeAbono.ReduceInstallment);

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        r.Value!.BalancesAfter.CreditBalance.Should().Be(0m);
        r.Value.BalancesAfter.Principal.Should().Be(2_500_000m);
        r.Value.TargetApplication!.NewPlan!.InstallmentCount.Should().Be(24, "reducir la cuota conserva el plazo");
        (await _p.Db.RepaymentScheduleVersions.AsNoTracking().CountAsync(v => v.LoanId == _p.Credito.Id)).Should().BeGreaterThan(1);
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Aplicar_a_capital_exige_un_saldo_a_favor_y_la_eleccion()
    {
        var sinEleccion = await ResolverAsync(FormaDeResolucion.ApplyAsExtraordinaryPrincipal, 1m);
        sinEleccion.Code.Should().Be("Validation.Invalid");
        var deAnticipo = await ResolverAsync(FormaDeResolucion.ApplyAsExtraordinaryPrincipal, 1m, ConceptoDeObligacion.Advance, eleccion: EleccionDeAbono.ReduceTerm);
        deAnticipo.Code.Should().Be("Validation.Invalid");
    }

    [Fact]
    public async Task Aplicarlo_a_otra_obligacion_del_mismo_deudor_es_un_pago_InternalOffset_con_la_clave_destino()
    {
        await SaldoAFavorAsync(400_000m);
        var otro = await _p.C.DesembolsarAsync(_p.D.O.Solicitud(monto: 1_000_000m, cuotas: 12));
        var clave = PagosDePrueba.Clave();

        var r = await ResolverAsync(FormaDeResolucion.ApplyToOtherObligation, 400_000m, destino: otro.PublicId, modalidad: ModalidadDePago.FreeAmount, clave: clave);

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        r.Value!.TargetApplication.Should().NotBeNull();
        var origen = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == r.Value.OperationPublicId);
        var destino = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == r.Value.TargetApplication!.OperationPublicId);
        destino.LoanId.Should().Be(otro.Id);
        destino.IdempotencyKey.Should().Be(clave + ":destino");
        destino.PaymentMeansClass.Should().Be(nameof(ClaseDeMedioDeRecaudo.InternalOffset));
        destino.PayerPersonId.Should().Be(_p.D.O.Ana.Id);
        destino.RelatedOperationId.Should().Be(origen.Id);
        (await SaldoListadoAsync()).Should().Be(0m);
        (await SaldoListadoAsync(otro.PublicId, ConceptoDeObligacion.Advance)).Should().Be(400_000m, "sin cuotas vencidas el valor libre queda como anticipo");
        await _p.ElLibroCuadraAsync();
        await _p.ElLibroCuadraAsync(otro.Id);
    }

    [Fact]
    public async Task Aplicarlo_a_la_obligacion_de_otra_persona_responde_TargetNotOwned()
    {
        await SaldoAFavorAsync(100_000m);
        var deBeto = await _p.C.DesembolsarAsync(_p.D.O.Solicitud(monto: 1_000_000m, cuotas: 12, solicitante: _p.D.O.Beto));

        var r = await ResolverAsync(FormaDeResolucion.ApplyToOtherObligation, 100_000m, destino: deBeto.PublicId, modalidad: ModalidadDePago.FreeAmount);

        r.Code.Should().Be("Lending.CreditBalance.TargetNotOwned");
        (await SaldoListadoAsync()).Should().Be(100_000m, "nada se escribió");
    }

    [Fact]
    public async Task Un_saldo_a_favor_en_una_obligacion_cancelada_se_resuelve_aqui()
    {
        await SaldoAFavorAsync(100_000m);
        var cancelacion = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FullPayoff, 3_000_000m));
        cancelacion.PaidOff.Should().BeTrue("la cancelación no usa el saldo a favor (D7)");
        (await SaldoListadoAsync()).Should().Be(100_000m, "una cancelada con saldo a favor se sigue listando");

        var r = await ResolverAsync(FormaDeResolucion.RefundThroughCollector, 100_000m);

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
    }
}
