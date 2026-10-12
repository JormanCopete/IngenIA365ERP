using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 013, US5, T0518 (mensajes.md §3, §7.2, §10.7–§10.14, §11.5; C01, C03, C04, C08, C14): lo que emite un pago. <c>PagoAplicado</c> (origen la
/// operación, <c>PaymentApplication</c>, clave <c>Confirmation</c>, tercero el pagador, <c>dimensions.collectorClass</c>) y en su misma unidad, cuando
/// corresponden, <c>AnticipoRecibido</c>, <c>RecuperacionDeCastigada</c>, <c>SaldoAFavorReconocido</c> y <c>AjusteDeCausacionPorFechaValor</c>
/// (<c>cause = ValueDatedOperation</c>), con el modo del principal; el anticipo al vencer en el ciclo (<c>AnticipoAplicado</c>, clave
/// <c>Confirmation:{crédito:N}</c>, filas de la corrida); <c>PagoReversado</c> con <c>related</c> = el pago y el modo heredado; ningún contenido trae
/// cuentas; <c>PagoAplicado</c> no lo origina el proceso.
/// </summary>
public class EmisionDePagosTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Un_anticipo_emite_PagoAplicado_con_AnticipoRecibido_en_su_unidad_y_al_vencer_el_ciclo_lo_aplica_con_AnticipoAplicado()
    {
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.Advance, 100_000m));

        c.AdvanceAmount.Should().Be(100_000m);
        c.Warnings.Should().Contain("Lending.Payment.AdvanceDoesNotReduceInterest");
        var op = await _p.OperacionAsync(c.OperationPublicId);
        var unidad = await _p.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == op.PublicId).ToListAsync();
        unidad.Select(m => m.Type).Should().BeEquivalentTo([PagoAplicadoV1.Type, AnticipoRecibidoV1.Type]);
        unidad.Should().OnlyContain(m => m.OriginEventKey == ClavesDeEvento.Confirmacion && m.OriginDocumentClass == "PaymentApplication"
                                         && m.PersonPublicId == _p.D.O.Ana.PublicId && m.OriginNumber == op.Number);
        unidad.Single(m => m.Type == AnticipoRecibidoV1.Type).PayloadJson.Should().Contain("\"concept\":\"Advance\"").And.Contain("\"amount\":100000");
        var modos = await _p.Db.IntegrationMessageDeliveries.AsNoTracking().Where(d => unidad.Select(m => m.Id).Contains(d.MessageId)).Select(d => d.Mode).ToListAsync();
        modos.Distinct().Should().ContainSingle("los acompañantes sellan el modo del principal (C01)");
        var filas = await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id).ToListAsync();
        filas.Should().ContainSingle(t => t.Kind == LoanTransactionKind.AdvanceReceived && t.Amount == 100_000m
                                          && t.MessagePublicId == unidad.Single(m => m.Type == AnticipoRecibidoV1.Type).PublicId);

        var vencimiento = (await _p.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate;
        await _p.CausarHastaAsync(vencimiento);

        var aplicado = await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == AnticipoAplicadoV1.Type);
        aplicado.OriginEventKey.Should().Be(ClavesDeEvento.ConfirmacionPor(_p.Credito.PublicId));
        aplicado.PayloadJson.Should().Contain($"\"valueDate\":\"{vencimiento:yyyy-MM-dd}\"").And.Contain("\"installmentNumber\":1")
            .And.Contain(op.PublicId.ToString(), "advanceSources nombra el pago que dejó el anticipo");
        var alVencer = await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.Kind == LoanTransactionKind.AdvanceApplied).ToListAsync();
        alVencer.Should().NotBeEmpty().And.OnlyContain(t => t.ProcessRunId != null && t.OperationId == null && t.ValueDate == vencimiento
                                                          && t.MessagePublicId == aplicado.PublicId);
        alVencer.Where(t => t.Concept == LoanConcept.Advance).Sum(t => t.Amount).Should().Be(-100_000m, "el anticipo cubre la cuota en su vencimiento");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Un_pago_con_fecha_valor_anterior_a_lo_causado_lleva_el_ajuste_de_causacion_en_su_unidad_y_el_libro_cuadra()
    {
        await _p.CausarHastaAsync(Hoy.AddDays(10));

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.ExtraordinaryPrincipal, 500_000m, Hoy.AddDays(7)));

        var op = await _p.OperacionAsync(c.OperationPublicId);
        var unidad = await _p.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == op.PublicId).ToListAsync();
        var ajuste = unidad.Should().ContainSingle(m => m.Type == AjusteDeCausacionPorFechaValorV1.Type).Subject;
        ajuste.OriginEventKey.Should().Be(ClavesDeEvento.Confirmacion);
        ajuste.PayloadJson.Should().Contain("\"cause\":\"ValueDatedOperation\"");
        var ajustes = await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id && t.Kind == LoanTransactionKind.AccrualAdjustment).ToListAsync();
        ajustes.Should().NotBeEmpty().And.OnlyContain(t => t.ValueDate > Hoy.AddDays(7) && t.ValueDate <= Hoy.AddDays(10) && t.MessagePublicId == ajuste.PublicId);
        ajustes.Sum(t => t.Amount).Should().BeLessThan(0m, "el abono baja el capital desde el día siguiente a su fecha valor: menos interés causado");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Un_pago_a_un_credito_castigado_emite_RecuperacionDeCastigada_y_escribe_filas_Recovery()
    {
        var credito = await _p.Db.Loans.SingleAsync(l => l.Id == _p.Credito.Id);
        credito.Status = LoanStatus.WrittenOff;
        credito.WrittenOffOn = Hoy;
        await _p.Db.SaveChangesAsync();

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FullPayoff, 3_000_000m));

        var op = await _p.OperacionAsync(c.OperationPublicId);
        (await _p.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == op.PublicId).Select(m => m.Type).ToListAsync())
            .Should().Contain(RecuperacionDeCastigadaV1.Type);
        (await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id).ToListAsync())
            .Should().Contain(t => t.Kind == LoanTransactionKind.Recovery);
        (await _p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _p.Credito.Id)).Status.Should().Be(LoanStatus.PaidOff, "el castigado se cancela con la recuperación");
    }

    [Fact]
    public async Task El_reverso_por_el_recaudador_emite_PagoReversado_con_el_pago_como_relacionado_y_hereda_su_modo()
    {
        var pago = _p.Pago(ModalidadDePago.FreeAmount, 300_000m);
        var c = await _p.AplicadoAsync(pago);

        var r = await _p.Contrato().ReversarAsync(new SolicitudDeReverso(PagosDePrueba.Prueba, pago.IdempotencyKey, "Cheque devuelto"), default);
        _p.Db.ChangeTracker.Clear();

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        var reversado = await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == PagoReversadoV1.Type);
        reversado.RelatedPublicId.Should().Be(c.OperationPublicId);
        reversado.OriginDocumentClass.Should().Be("PaymentReversal");
        reversado.PayloadJson.Should().Contain("\"initiatedBy\":\"Collector\"").And.Contain("\"reason\":\"Cheque devuelto\"");
        (await _p.Db.IntegrationMessages.AnyAsync(m => m.Type == PagoReversadoPorCarteraV1.Type))
            .Should().BeFalse("el reverso lo pidió el recaudador: no se le avisa (§3.8)");
        var original = await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == PagoAplicadoV1.Type);
        var modoOriginal = await _p.Db.IntegrationMessageDeliveries.AsNoTracking().Where(d => d.MessageId == original.Id).Select(d => d.Mode).SingleAsync();
        (await _p.Db.IntegrationMessageDeliveries.AsNoTracking().Where(d => d.MessageId == reversado.Id).Select(d => d.Mode).SingleAsync())
            .Should().Be(modoOriginal, "el reverso hereda el modo del pago (C08)");
        var espejos = await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.ReversesTransactionId != null).ToListAsync();
        espejos.Should().NotBeEmpty().And.OnlyContain(t => t.MessagePublicId == reversado.PublicId);
    }

    [Fact]
    public async Task PagoAplicado_no_lo_origina_el_proceso()
    {
        _p.G.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Actor.ProcesoDeIntegracion("Tarea:prueba")));

        var aplicar = () => _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1_000m));

        await aplicar.Should().ThrowAsync<InvalidOperationException>("un hecho que no admite proceso no lo emite el proceso (T11, C14)");
    }

    [Fact]
    public async Task Pagar_lo_vencido_de_un_credito_suspendido_cobra_el_suspenso_y_reanuda_la_causacion_aparte()
    {
        await _p.C.AnexoAsync(Hoy);
        await _p.C.SembrarTiposDeHechoAsync();
        var vencimiento = (await _p.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate;
        await _p.CausarHastaAsync(vencimiento.AddDays(70));
        var listado = await _p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, _p.D.O.Ana.PublicId, _p.G.HoyLocal), default);
        var debe = listado.Value!.Obligations[0];
        debe.DaysPastDue.Should().BeGreaterThan(60);

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.PastDue, debe.DueToday));

        var op = await _p.OperacionAsync(c.OperationPublicId);
        var tipos = await _p.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == op.PublicId).Select(m => new { m.Type, m.OriginEventKey }).ToListAsync();
        tipos.Should().Contain(t => t.Type == CobroDeInteresEnSuspensoV1.Type && t.OriginEventKey == ClavesDeEvento.Confirmacion);
        tipos.Should().Contain(t => t.Type == CausacionReanudadaV1.Type && t.OriginEventKey != ClavesDeEvento.Confirmacion,
            "la reanudación va aparte, con Confirmation:{tipoDeHecho:N}");
        c.BalancesAfter.DaysPastDue.Should().Be(0);
        await _p.ElLibroCuadraAsync();
    }
}
