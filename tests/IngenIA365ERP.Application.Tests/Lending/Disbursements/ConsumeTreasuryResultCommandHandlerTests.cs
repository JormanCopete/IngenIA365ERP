using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 013, US3, T0420 (interfaces.md §4.3, una prueba por fila; data-model §7.5, §7.11; C08, C49, C113, SC-010): el resultado del giro que
/// emite la tesorería de prueba se consume con la bandeja real (<c>MensajesEntrantes</c>). Girada: sin efecto en el libro; rechazada, anulada (con la
/// anulación pedida) y cheque no cobrado: el reverso desde su origen con espejos, <c>DesembolsoReversado</c> que hereda el modo y el crédito
/// pendiente de giro; lo que no cabe, un solo código <c>Lending.DisbursementOrder.InvalidResult</c> con el motivo como dato; la orden inexistente,
/// <c>Generic.NotFound</c>; cada resultado aceptado deja su fila y su recibo con su evaluación, y repetir el mensaje no cambia nada.
/// </summary>
public sealed class ConsumeTreasuryResultCommandHandlerTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private DisbursementResultDto _desembolso = null!;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        var s = await _d.AprobadaAsync();
        _desembolso = await _d.DesembolsadoAsync(_d.Entrada(s));
    }

    public async Task InitializeConChequeAsync()
    {
        _d.Dispose();
        _d = await DesembolsoDePrueba.CrearAsync();
        var s = await _d.AprobadaAsync();
        _desembolso = await _d.DesembolsadoAsync(_d.Entrada(s) with { Form = DisbursementForm.Check, Beneficiary = null });
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> EmitirAsync(string resultado, decimal? valor = null, Guid? orden = null)
    {
        var r = await _d.EmitirResultado().Handle(new EmitTestDisbursementResultCommand(orden ?? _desembolso.Order!.PublicId, resultado, "TRF-1",
            DesembolsoDePrueba.Hoy, valor, "Cuenta inexistente") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _d.Db.ChangeTracker.Clear();
        return r.Value.MessagePublicId;
    }

    private async Task<ResultadoDeConsumo> ConsumirAsync(Guid mensaje)
    {
        var r = await _d.Consumir().Handle(new ConsumeTreasuryResultCommand([mensaje]), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _d.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private Task<Domain.Entities.Lending.Operations.DisbursementOrder> OrdenAsync() => _d.Db.DisbursementOrders.AsNoTracking().SingleAsync();

    [Fact]
    public async Task Emitida_a_girada_no_toca_el_libro()
    {
        var r = await ConsumirAsync(await EmitirAsync("Disbursed"));

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await OrdenAsync()).Status.Should().Be(DisbursementOrderStatus.Disbursed);
        (await _d.Db.LoanOperations.CountAsync()).Should().Be(1);
        (await _d.Db.Loans.SingleAsync()).Status.Should().Be(LoanStatus.Active);
        var resultado = await _d.Db.DisbursementOrderResults.SingleAsync();
        (resultado.Result, resultado.ReversalOperationId).Should().Be((DisbursementOrderStatus.Disbursed, (long?)null));
    }

    [Fact]
    public async Task Emitida_a_rechazada_reversa_desde_el_origen_y_deja_el_credito_pendiente_de_giro()
    {
        var mensaje = await EmitirAsync("Rejected");

        var r = await ConsumirAsync(mensaje);

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var reverso = await _d.Db.LoanOperations.SingleAsync(o => o.Kind == LoanOperationKind.DisbursementReversal);
        var original = await _d.Db.LoanOperations.SingleAsync(o => o.Kind == LoanOperationKind.Disbursement);
        (reverso.ReversesOperationId, original.ReversedByOperationId).Should().Be((original.Id, reverso.Id));
        (reverso.ActorKind, reverso.ActorUserId).Should().Be((ActorKind.Process, (int?)null), "lo hace el proceso con el usuario de Tesorería como origen");
        reverso.OriginUserCentralId.Should().Be(_d.Cajera.CentralUserId);
        var espejo = await _d.Db.LoanTransactions.SingleAsync(t => t.ReversesTransactionId != null);
        (espejo.Amount, espejo.OperationId).Should().Be((-3_000_000m, (long?)reverso.Id));
        (await _d.Db.Loans.SingleAsync()).Status.Should().Be(LoanStatus.PendingDisbursement);
        (await OrdenAsync()).Status.Should().Be(DisbursementOrderStatus.Rejected);
        (await _d.Db.DisbursementOrderResults.SingleAsync()).ReversalOperationId.Should().Be(reverso.Id);

        var hecho = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == DesembolsoReversadoV1.Type);
        hecho.RelatedPublicId.Should().Be(original.PublicId);
        var c = JsonDocument.Parse(hecho.PayloadJson).RootElement;
        (c.GetProperty("cause").GetString(), c.GetProperty("loanStatusAfter").GetString()).Should().Be(("Rejected", "PendingDisbursement"));
        c.GetProperty("treasuryResultMessageId").GetGuid().Should().Be(mensaje);
        c.GetProperty("original").GetProperty("type").GetString().Should().Be(CreditoDesembolsadoV1.Type);
        var desembolso = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoDesembolsadoV1.Type);
        var entregas = await _d.Db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(e => e.MessageId == hecho.Id || e.MessageId == desembolso.Id).Select(e => e.Mode).Distinct().ToListAsync();
        entregas.Should().ContainSingle("el reverso hereda el modo del desembolso (C08)");
    }

    [Fact]
    public async Task Con_la_anulacion_pedida_anulada_reversa_y_girada_no()
    {
        var anular = await _d.AnularOrden().Handle(new VoidDisbursementOrderCommand(_desembolso.Order!.PublicId, "Otra cuenta") { OperationKey = Guid.NewGuid() }, default);
        anular.IsSuccess.Should().BeTrue(anular.IsFailure ? anular.Error.Code : string.Empty);
        _d.Db.ChangeTracker.Clear();

        (await ConsumirAsync(await EmitirAsync("Voided"))).Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await _d.Db.Loans.SingleAsync()).Status.Should().Be(LoanStatus.PendingDisbursement);
        var hecho = JsonDocument.Parse((await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == DesembolsoReversadoV1.Type)).PayloadJson).RootElement;
        hecho.GetProperty("cause").GetString().Should().Be("Voided");
    }

    [Fact]
    public async Task Con_la_anulacion_pedida_girada_no_reversa()
    {
        await _d.AnularOrden().Handle(new VoidDisbursementOrderCommand(_desembolso.Order!.PublicId, "Otra cuenta") { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();

        (await ConsumirAsync(await EmitirAsync("Disbursed"))).Should().BeOfType<ResultadoDeConsumo.Processed>();

        (await OrdenAsync()).Status.Should().Be(DisbursementOrderStatus.Disbursed);
        (await _d.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.DisbursementReversal)).Should().Be(0);
    }

    [Fact]
    public async Task Un_cheque_girado_y_no_cobrado_reversa_el_desembolso()
    {
        await InitializeConChequeAsync();
        await ConsumirAsync(await EmitirAsync("Disbursed"));

        (await ConsumirAsync(await EmitirAsync("CheckNotCashed"))).Should().BeOfType<ResultadoDeConsumo.Processed>();

        (await OrdenAsync()).Status.Should().Be(DisbursementOrderStatus.CheckNotCashed);
        (await _d.Db.Loans.SingleAsync()).Status.Should().Be(LoanStatus.PendingDisbursement);
        (await _d.Db.DisbursementOrderResults.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Un_valor_distinto_del_neto_es_InvalidResult_con_AmountMismatch_y_queda_reprocesable()
    {
        var r = await ConsumirAsync(await EmitirAsync("Disbursed", valor: 2_999_000m));

        var rechazo = r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Subject;
        rechazo.Code.Should().Be(ErroresDeDesembolso.CodigoResultadoInvalido);
        var datos = JsonDocument.Parse(rechazo.DataJson!).RootElement;
        (datos.GetProperty("reason").GetString(), datos.GetProperty("expected").GetDecimal(), datos.GetProperty("received").GetDecimal())
            .Should().Be(("AmountMismatch", 3_000_000m, 2_999_000m));
        var recibo = await _d.Db.LendingInboundMessages.SingleAsync();
        recibo.Status.Should().Be(InboundMessageStatus.Rejected);
        (await _d.Db.LendingInboundMessageEvaluations.SingleAsync()).Retryable.Should().BeTrue();
        (await OrdenAsync()).Status.Should().Be(DisbursementOrderStatus.Issued);
    }

    [Fact]
    public async Task Una_transicion_que_no_cabe_es_InvalidResult_con_InvalidTransition()
    {
        var r = await ConsumirAsync(await EmitirAsync("CheckNotCashed"));

        var rechazo = r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Subject;
        rechazo.Code.Should().Be(ErroresDeDesembolso.CodigoResultadoInvalido);
        var datos = JsonDocument.Parse(rechazo.DataJson!).RootElement;
        (datos.GetProperty("reason").GetString(), datos.GetProperty("from").GetString(), datos.GetProperty("to").GetString())
            .Should().Be(("InvalidTransition", "Issued", "CheckNotCashed"));
    }

    [Fact]
    public async Task Una_orden_inexistente_es_Generic_NotFound()
    {
        var mensaje = await EmitirAsync("Disbursed");
        var orden = await _d.Db.DisbursementOrders.SingleAsync();
        orden.IsDeleted = true;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var r = await ConsumirAsync(mensaje);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Cada_resultado_aceptado_deja_su_recibo_y_repetir_el_mensaje_no_cambia_nada()
    {
        var mensaje = await EmitirAsync("Rejected");
        await ConsumirAsync(mensaje);

        var otra = await ConsumirAsync(mensaje);

        otra.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        (await _d.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.DisbursementReversal)).Should().Be(1);
        (await _d.Db.DisbursementOrderResults.CountAsync()).Should().Be(1);
        var recibo = await _d.Db.LendingInboundMessages.SingleAsync();
        (recibo.Status, recibo.MessagePublicId, recibo.EvaluationCount).Should().Be((InboundMessageStatus.Processed, mensaje, 1));
        (await _d.Db.LendingInboundMessageEvaluations.SingleAsync()).Outcome.Should().Be(InboundEvaluationOutcome.Accepted);
    }

    [Fact]
    public async Task El_reverso_de_un_desembolso_cuyo_dinero_ya_salio_se_rechaza()
    {
        await ConsumirAsync(await EmitirAsync("Disbursed"));
        var orden = await _d.Db.DisbursementOrders.AsNoTracking().SingleAsync();

        var r = await _d.Reverso().ReversarAsync(orden, ReversoDeDesembolso.CausaAnulada, null, null, "manual",
            new ActorDelReverso(ActorKind.Person, _d.Cajera.Id, _d.Cajera.CentralUserId, null), default);

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoDineroYaSalio);
    }

    [Fact]
    public async Task Con_dos_tramos_se_reversa_solo_el_capital_del_tramo_y_sale_el_ajuste_de_causacion()
    {
        _d.Dispose();
        _d = await DesembolsoDePrueba.CrearAsync(Origination.OriginacionDePrueba.VersionDeLinea() with { Amortization = new AmortizationInput { AllowsTranches = true } });
        var motor = Substitute.For<IMotorDeCreditoEnDesembolso>();
        motor.LiquidarAsync(default, default, default).ReturnsForAnyArgs(new LiquidacionParaRecogida(0m, 0, false));
        motor.RegistrarPuestaAlDiaAsync(default!, default)
            .ReturnsForAnyArgs(Result.Success<IReadOnlyList<Domain.Entities.Integration.Transactions.IntegrationMessage>>([]));
        motor.ConciliarCausacionAsync(default, default, default!, default, default).ReturnsForAnyArgs(ci =>
            Task.FromResult<IReadOnlyList<LoanTransaction>>(
            [
                new LoanTransaction
                {
                    LoanId = ci.ArgAt<int>(0), Operation = ci.ArgAt<Domain.Entities.Lending.Operations.LoanOperation>(2), Kind = LoanTransactionKind.AccrualAdjustment,
                    Concept = LoanConcept.CurrentInterest, InstallmentNumber = 1, Amount = -1_234.56m, ValueDate = ci.ArgAt<DateOnly>(1),
                    AccountingDate = ci.ArgAt<DateOnly>(3), RegisteredAt = ci.ArgAt<Domain.Entities.Lending.Operations.LoanOperation>(2).RegisteredAt,
                },
            ]));
        _d.Motor = motor;
        var s = await _d.AprobadaAsync();
        var primero = await _d.DesembolsadoAsync(_d.Entrada(s) with { TrancheAmount = 2_000_000m });
        _desembolso = await _d.DesembolsadoAsync(_d.Entrada(s) with { LoanPublicId = primero.LoanPublicId, TrancheAmount = 1_000_000m });

        await ConsumirAsync(await EmitirAsync("Rejected"));

        var espejos = await _d.Db.LoanTransactions.Where(t => t.ReversesTransactionId != null).ToListAsync();
        espejos.Should().ContainSingle().Which.Amount.Should().Be(-1_000_000m);
        (await _d.Db.LoanTransactions.CountAsync(t => t.Kind == LoanTransactionKind.AccrualAdjustment)).Should().Be(1);
        (await _d.Db.Loans.SingleAsync()).Status.Should().Be(LoanStatus.Active, "le queda el otro tramo vivo");
        var principal = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == DesembolsoReversadoV1.Type);
        var acompanante = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == AjusteDeCausacionPorFechaValorV1.Type);
        (acompanante.OriginPublicId, acompanante.OriginEventKey).Should().Be((principal.OriginPublicId, principal.OriginEventKey), "viaja en la unidad del reverso (C49)");
    }
}
