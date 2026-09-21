using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.ServiceBonus;

/// <summary>
/// T041 (feature 010, US1; FR-032): reversar una prima aprobada deja el asiento espejo con el mismo
/// origen, libera el saldo inicial que consumió y admite liquidar el semestre de nuevo; con una marca
/// de pago vigente no se reversa; descartar un borrador lo deja <c>Superseded</c> con motivo y sin
/// contabilidad. Todo por los comandos propios de la prima con <c>Kind = ServiceBonus</c>.
/// </summary>
public class ReverseServiceBonusCommandHandlerTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private static async Task<(NominaTestData D, Guid RunId)> PrimaAprobadaAsync(bool conSaldoInicial = false)
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        if (conSaldoInicial) d.SaldoInicial(d.Ana, new DateOnly(2025, 12, 31), prima: 300_000m, diasPrima: 45);
        var calculo = await PrimaDePrueba.Calcular(d).Handle(new CalculateServiceBonusCommand(2026, 1), CancellationToken.None);
        calculo.IsSuccess.Should().BeTrue(calculo.Error.Message);
        var aprobada = await PrimaDePrueba.Aprobar(d, Contadora).Handle(new ApproveServiceBonusCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);
        aprobada.IsSuccess.Should().BeTrue(aprobada.Error.Message);
        return (d, calculo.Value.RunPublicId);
    }

    [Fact]
    public async Task Reversar_deja_el_espejo_ServiceBonusRun_libera_el_saldo_inicial_y_admite_liquidar_de_nuevo()
    {
        var (d, runId) = await PrimaAprobadaAsync(conSaldoInicial: true);
        var saldo = await d.Db.EmployeeBenefitOpeningBalances.SingleAsync();
        saldo.ConsumedByRunId.Should().NotBeNull("la aprobación lo consumió");

        var r = await PrimaDePrueba.Reversar(d, Contadora).Handle(new ReverseServiceBonusCommand(runId, "Faltó una comisión de junio"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.ReversalNumber.Should().Be("NM-2");
        var run = await d.Db.PayrollRuns.Include(x => x.AccountingDocument).Include(x => x.ReversalAccountingDocument).SingleAsync(x => x.PublicId == runId);
        run.Status.Should().Be(PayrollRunStatus.Reversed);
        run.ReversedBy.Should().Be("contadora@demo");
        run.ReversalReason.Should().Be("Faltó una comisión de junio");
        run.ReversalAccountingDocument!.SourceType.Should().Be("ServiceBonusRun");
        run.ReversalAccountingDocument.TotalDebit.Should().Be(run.AccountingDocument!.TotalDebit, "espejo del original");
        run.ReversalAccountingDocument.Date.Should().Be(d.Clock.TodayUtc, "el espejo se fecha hoy");
        (await d.Db.EmployeeBenefitOpeningBalances.SingleAsync()).ConsumedByRunId.Should().BeNull("liberado: vuelve a ser editable");

        var otra = await PrimaDePrueba.Calcular(d).Handle(new CalculateServiceBonusCommand(2026, 1), CancellationToken.None);
        otra.IsSuccess.Should().BeTrue("tras reversar, el semestre se liquida de nuevo (FR-005)");
        otra.Value.Version.Should().Be(2);

        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollSettlementReversed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_una_marca_de_pago_vigente_no_se_reversa_y_al_retirarla_si()
    {
        var (d, runId) = await PrimaAprobadaAsync();
        var run = await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId);
        var fila = await d.Db.PayrollRunEmployees.SingleAsync(e => e.PayrollRunId == run.Id);
        var pago = new PayrollPayment { PayrollRunEmployeeId = fila.Id, PaidAt = d.Clock.UtcNow, PaidBy = "tesorera", CreatedBy = "test" };
        d.Db.PayrollPayments.Add(pago);
        await d.Db.SaveChangesAsync();

        var bloqueada = await PrimaDePrueba.Reversar(d, Contadora).Handle(new ReverseServiceBonusCommand(runId, "x"), CancellationToken.None);
        bloqueada.Error.Code.Should().Be("Payroll.PaymentBlocksReversal");
        (await d.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == runId)).Status.Should().Be(PayrollRunStatus.Approved);

        pago.IsReverted = true;
        await d.Db.SaveChangesAsync();
        (await PrimaDePrueba.Reversar(d, Contadora).Handle(new ReverseServiceBonusCommand(runId, "Pago retirado, se corrige"), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Reversar_exige_aprobada_motivo_y_el_tipo_de_la_ruta()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var borrador = await PrimaDePrueba.Calcular(d).Handle(new CalculateServiceBonusCommand(2026, 1), CancellationToken.None);

        (await PrimaDePrueba.Reversar(d, Contadora).Handle(new ReverseServiceBonusCommand(borrador.Value.RunPublicId, "x"), CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.NotApproved");
        (await PrimaDePrueba.Reversar(d, Contadora).Handle(new ReverseServiceBonusCommand(borrador.Value.RunPublicId, "  "), CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.ReasonRequired");
        (await PrimaDePrueba.Reversar(d, Contadora).Handle(new ReverseServiceBonusCommand(Guid.NewGuid(), "x"), CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Run.NotFound");

        var cesantias = await LiquidacionDePrueba.CesantiasAsync(d);
        (await PrimaDePrueba.Reversar(d, Contadora).Handle(new ReverseServiceBonusCommand(cesantias.PublicId, "x"), CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.KindMismatch", "la ruta de la prima no toca una liquidación de cesantías");
        new ReverseServiceBonusCommandValidator().Validate(new ReverseServiceBonusCommand(Guid.NewGuid(), string.Empty)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Descartar_deja_Superseded_con_motivo_sin_contabilidad_y_el_semestre_se_calcula_de_nuevo()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        var borrador = await PrimaDePrueba.Calcular(d).Handle(new CalculateServiceBonusCommand(2026, 1), CancellationToken.None);

        var r = await PrimaDePrueba.Descartar(d, Contadora).Handle(new DiscardServiceBonusCommand(borrador.Value.RunPublicId, "Se calculó con el salario viejo"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var run = await d.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == borrador.Value.RunPublicId);
        run.Status.Should().Be(PayrollRunStatus.Superseded);
        run.DiscardedBy.Should().Be("contadora@demo");
        run.DiscardReason.Should().Be("Se calculó con el salario viejo");
        (await d.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollSettlementDiscarded), Arg.Any<CancellationToken>());

        var otra = await PrimaDePrueba.Calcular(d).Handle(new CalculateServiceBonusCommand(2026, 1), CancellationToken.None);
        otra.IsSuccess.Should().BeTrue(otra.Error.Message);
        otra.Value.Version.Should().Be(2);
        (await PrimaDePrueba.Descartar(d, Contadora).Handle(new DiscardServiceBonusCommand(borrador.Value.RunPublicId, "otra vez"), CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Settlement.NotDraft");
    }
}
