using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// Feature 013, US4, T0472 (T6, C04; data-model §22.3, §22.4): una operación sobre un crédito con la marca anterior a su fecha valor —aquí, el segundo
/// tramo cinco días después del primero— lo pone al día en <b>su propia transacción</b> antes de aplicar: la causación día por día con partes
/// <c>LoanCatchUp</c> (una por tipo, <c>TriggerOperationId</c> = el tramo, <c>catchUpOperation</c> en el contenido, <c>batchNumber = 1</c>), las filas
/// con la operación como origen y el mensaje de su parte, y la marca en la fecha valor; el plan del tramo se regenera sobre lo desembolsado. La
/// tanda del ciclo de ese día ya no lo causa: lo cuenta como saltado.
/// </summary>
public class PuestaAlDiaDeCreditoTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync() =>
        _c = await CausacionDePrueba.CrearAsync(conDesembolso: false, version: OriginacionDePrueba.VersionDeLinea() with
        {
            Amortization = new AmortizationInput { AllowsTranches = true },
        });

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task El_tramo_siguiente_pone_al_dia_el_credito_con_partes_LoanCatchUp_y_la_tanda_del_dia_lo_salta()
    {
        var s = await _c.D.AprobadaAsync();
        var primero = await _c.D.DesembolsadoAsync(_c.D.Entrada(s) with { TrancheAmount = 2_000_000m });
        var d = Hoy.AddDays(5);
        _c.Ir(d);

        var segundo = await _c.D.DesembolsadoAsync(_c.D.Entrada(s) with { LoanPublicId = primero.LoanPublicId, TrancheAmount = 1_000_000m });

        var tramo = await _c.Db.LoanOperations.SingleAsync(o => o.PublicId == segundo.OperationPublicId);
        var credito = tramo.LoanId;
        (await _c.ControlAsync(credito)).LastAccruedDate.Should().Be(d, "puesto al día hasta la fecha valor del tramo");
        var filas = await _c.Db.LoanTransactions.Where(t => t.Kind == LoanTransactionKind.Accrual).OrderBy(t => t.ValueDate).ToListAsync();
        filas.Select(f => f.ValueDate).Should().Equal(Enumerable.Range(1, 5).Select(i => Hoy.AddDays(i)));
        filas.Should().OnlyContain(f => f.OperationId == tramo.Id && f.ProcessRunId == null, "la operación es el origen de la puesta al día");

        var parte = await _c.Db.AccountingFactParts.SingleAsync();
        parte.Should().BeEquivalentTo(new
        {
            SourceKind = AccountingFactPartSource.LoanCatchUp, ProcessRunId = (int?)null, BatchNumber = (int?)null, TriggerOperationId = (long?)tramo.Id,
            FactType = InteresCausadoV1.Type, ValueDate = d,
        });
        filas.Should().OnlyContain(f => f.MessagePublicId == parte.MessagePublicId);
        var mensaje = await _c.Db.IntegrationMessages.SingleAsync(m => m.PublicId == parte.MessagePublicId);
        mensaje.OriginNumber.Should().Be($"ALD-{tramo.Number}-1", "el rótulo de la puesta al día nombra la operación que la pidió (mensajes.md §3)");
        mensaje.PayloadJson.Should().Contain("\"catchUpOperation\":{").And.Contain(tramo.PublicId.ToString())
            .And.Contain("\"batchNumber\":1").And.Contain("\"runKind\":null").And.Contain($"\"valueDate\":\"{Hoy.AddDays(1):yyyy-MM-dd}\"");
        (await _c.Db.LoanDailyPositions.CountAsync(p => p.LoanId == credito && p.PositionDate > Hoy && p.PositionDate < d)).Should().Be(4);

        var plan = await _c.Db.RepaymentScheduleVersions.SingleAsync(v => v.LoanId == credito && v.VersionNumber == 2);
        plan.PrincipalBase.Should().Be(3_000_000m);

        var corrida = await _c.CorrerCicloAsync(await _c.CicloAsync(d));
        (corrida.ProcessedLoans, corrida.SkippedLoans).Should().Be((0, 1), "la tanda del día lo cuenta como saltado");
        (await _c.Db.LoanTransactions.CountAsync(t => t.Kind == LoanTransactionKind.Accrual)).Should().Be(5);
    }

    [Fact]
    public async Task Reversar_el_segundo_tramo_concilia_la_causacion_desde_su_fecha_valor_y_el_libro_vuelve_a_cuadrar()
    {
        var s = await _c.D.AprobadaAsync();
        var primero = await _c.D.DesembolsadoAsync(_c.D.Entrada(s) with { TrancheAmount = 2_000_000m });
        var tramo = Hoy.AddDays(5);
        _c.Ir(tramo);
        var segundo = await _c.D.DesembolsadoAsync(_c.D.Entrada(s) with { LoanPublicId = primero.LoanPublicId, TrancheAmount = 1_000_000m });
        var d = Hoy.AddDays(10);
        await _c.CausarHastaAsync(d);
        var orden = await _c.Db.DisbursementOrders.SingleAsync(o => o.PublicId == segundo.Order!.PublicId);

        var r = await _c.D.Reverso().ReversarAsync(orden, Application.Lending.Disbursements.ReversoDeDesembolso.CausaRechazada, null, null, "rechazado",
            new Application.Lending.Disbursements.ActorDelReverso(Domain.Enums.Integration.ActorKind.Person, _c.D.Cajera.Id, _c.D.Cajera.CentralUserId, null),
            default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        _c.Db.ChangeTracker.Clear();
        var ajustes = await _c.Db.LoanTransactions.Where(t => t.OperationId == r.Value.Reverso.Id && t.Kind == LoanTransactionKind.AccrualAdjustment).ToListAsync();
        ajustes.Should().NotBeEmpty().And.OnlyContain(t => t.ValueDate > tramo && t.ValueDate <= d && t.Amount < 0m,
            "la causación del capital del tramo se devuelve desde el día siguiente a su fecha valor (C49)");
        var cargado = await _c.Cargador().CargarAsync(r.Value.Reverso.LoanId, null, d, default);
        var libro = (await _c.Cargador().LibroAsync([r.Value.Reverso.LoanId], d, default))[r.Value.Reverso.LoanId];
        Domain.Lending.MotorDeCartera.Verificar(cargado!.Entrada!, d, libro).Should().BeEmpty();
    }
}
