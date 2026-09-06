using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Payroll.Payments;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.ReversePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Runs;

/// <summary>T126 — FR-032: la reversión deja asiento espejo, corrida reversada, período abierto y novedades intactas; los pagos vigentes la bloquean.</summary>
public class ReversePayrollRunCommandHandlerTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);
    private static readonly ICurrentUserService Gerente = NominaTestData.UsuarioDePrueba("gerente@demo", 12);

    private static async Task<Guid> Aprobada(NominaTestData d, bool conRecurrente = false)
    {
        d.ConfigurarContabilidad();
        if (conRecurrente)
        {
            var c = await new CreateRecurringNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker)
                .Handle(new CreateRecurringNoveltyCommand(d.Ana.PublicId, "LIBRANZA", null, 80_000m, new DateTime(2026, 1, 1), null, 6, null), CancellationToken.None);
            c.IsSuccess.Should().BeTrue(c.Error.Message);
        }
        var horas = await new RegisterNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver)
            .Handle(new RegisterNoveltyCommand { PeriodPublicId = d.Marzo.PublicId, EmployeePublicId = d.Ana.PublicId, ConceptCode = "HEX_NOCTURNA", Quantity = 4m }, CancellationToken.None);
        horas.IsSuccess.Should().BeTrue(horas.Error.Message);

        var calc = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        var poster = new PayrollAccountingPoster(d.Db, d.Clock, Contadora);
        var audit = new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance);
        var apr = await new ApprovePayrollRunCommandHandler(d.Db, poster, d.Policies, d.Permissions, d.Clock, Contadora, audit)
            .Handle(new ApprovePayrollRunCommand(calc.Value.RunPublicId, Confirm: true), CancellationToken.None);
        apr.IsSuccess.Should().BeTrue(apr.Error.Message);
        return calc.Value.RunPublicId;
    }

    private static ReversePayrollRunCommandHandler Reversor(NominaTestData d) =>
        new(d.Db, new PayrollAccountingPoster(d.Db, d.Clock, Gerente), d.Clock, Gerente,
            new PayrollAuditEmitter(d.Audit, Gerente, d.Clock, NullLogger<PayrollAuditEmitter>.Instance));

    [Fact]
    public async Task Reversar_deja_asiento_espejo_corrida_reversada_periodo_abierto_y_novedades_intactas()
    {
        var d = new NominaTestData();
        var runId = await Aprobada(d, conRecurrente: true);
        (await d.Db.PayrollRecurringNovelties.SingleAsync()).InstallmentsIssued.Should().Be(1);
        var novedadesAntes = await d.Db.PayrollNovelties.Select(n => new { n.Id, n.Status }).ToListAsync();

        var r = await Reversor(d).Handle(new ReversePayrollRunCommand(runId, "Faltó la incapacidad de Ana"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.ReversalAccountingDocumentNumber.Should().Be("NM-2");

        var run = await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId);
        run.Status.Should().Be(PayrollRunStatus.Reversed);
        run.ReversedBy.Should().Be("gerente@demo");
        run.ReversalReason.Should().Be("Faltó la incapacidad de Ana");
        run.ReversalAccountingDocumentId.Should().NotBeNull().And.NotBe(run.AccountingDocumentId);
        run.AccountingDocumentId.Should().NotBeNull("el comprobante original se conserva");

        var original = await d.Db.AccountingDocuments.SingleAsync(x => x.Id == run.AccountingDocumentId);
        var reverso = await d.Db.AccountingDocuments.SingleAsync(x => x.Id == run.ReversalAccountingDocumentId);
        reverso.TotalDebit.Should().Be(original.TotalCredit);
        reverso.TotalCredit.Should().Be(original.TotalDebit);
        reverso.Detail.Should().Contain("NM-1").And.Contain("Faltó la incapacidad de Ana");
        var asientosReverso = await d.Db.JournalEntries.Where(j => j.VoucherTypeCode == "NM" && j.DocumentNumber == 2).ToListAsync();
        asientosReverso.Sum(a => a.DebitAmount).Should().Be(asientosReverso.Sum(a => a.CreditAmount)).And.Be(original.TotalDebit);

        var periodo = await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id);
        periodo.Status.Should().Be(PayPeriodStatus.Open);
        periodo.ApprovedAt.Should().BeNull();
        periodo.StatusMessage.Should().Contain("Reversado por gerente@demo");

        (await d.Db.PayrollNovelties.Select(n => new { n.Id, n.Status }).ToListAsync()).Should().BeEquivalentTo(novedadesAntes, "las novedades no se tocan");
        (await d.Db.PayrollRecurringNovelties.SingleAsync()).InstallmentsIssued.Should().Be(0, "la cuota se devuelve");
        (await d.Db.PayrollRunLines.CountAsync()).Should().BeGreaterThan(0, "las líneas de la corrida reversada se conservan (Principio XI)");
        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollRunReversed), Arg.Any<CancellationToken>());

        // Y el período se puede volver a calcular: versión nueva, la reversada queda en el historial.
        var calc = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        calc.Value.Version.Should().Be(2);
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId)).Status.Should().Be(PayrollRunStatus.Reversed);
    }

    [Fact]
    public async Task Un_pago_vigente_bloquea_la_reversion_y_nombra_a_los_pagados()
    {
        var d = new NominaTestData();
        var runId = await Aprobada(d);
        var marca = await new MarkPaymentsCommandHandler(d.Db, d.Clock, Contadora, new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance))
            .Handle(new MarkPaymentsCommand(runId, null, new DateTime(2026, 3, 30), PayrollPaymentMethod.Transfer, null), CancellationToken.None);
        marca.Value.Should().Be(1);

        var r = await Reversor(d).Handle(new ReversePayrollRunCommand(runId, "error"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.PaymentBlocksReversal");
        r.Error.Message.Should().Contain("Ana Prueba");
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId)).Status.Should().Be(PayrollRunStatus.Approved);

        // Retirada la marca, se puede.
        await new RevertPaymentMarkCommandHandler(d.Db, d.Clock, Contadora, new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance))
            .Handle(new RevertPaymentMarkCommand(runId, d.Ana.PublicId, "transferencia devuelta"), CancellationToken.None);
        (await Reversor(d).Handle(new ReversePayrollRunCommand(runId, "error"), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Un_periodo_contable_cerrado_impide_la_reversion()
    {
        var d = new NominaTestData();
        var runId = await Aprobada(d);
        (await d.Db.AccountingPeriods.SingleAsync()).Status = "C";
        await d.Db.SaveChangesAsync();

        var r = await Reversor(d).Handle(new ReversePayrollRunCommand(runId, "tarde"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.AccountingPeriodClosedForReversal");
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId)).Status.Should().Be(PayrollRunStatus.Approved);
        (await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id)).Status.Should().Be(PayPeriodStatus.Approved);
    }

    [Fact]
    public async Task Sin_motivo_no_se_reversa_y_un_borrador_tampoco()
    {
        var d = new NominaTestData();
        var runId = await Aprobada(d);

        new ReversePayrollRunCommandValidator().Validate(new ReversePayrollRunCommand(runId, "")).IsValid.Should().BeFalse();
        (await Reversor(d).Handle(new ReversePayrollRunCommand(runId, "   "), CancellationToken.None)).Error.Code.Should().Be("Payroll.ReasonRequired");

        var d2 = new NominaTestData();
        var borrador = d2.Borrador(d2.Marzo);
        (await Reversor(d2).Handle(new ReversePayrollRunCommand(borrador.PublicId, "x"), CancellationToken.None)).Error.Code.Should().Be("Payroll.RunNotApproved");
    }
}
