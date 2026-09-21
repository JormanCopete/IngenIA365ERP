using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;

/// <summary>
/// Feature 010, US3 (FR-020, T065; research R14): aprobar la definitiva contabiliza por el ciclo común,
/// aplica cada descuento de Cartera con el <b>recaudo real</b> (<c>RecaudoDeCredito</c>: cuotas, transacción
/// RC y comprobante RC por el contrato) con el valor aplicado, cierra la ficha, apaga la bandera de
/// empleado, deja el movimiento de vacaciones y adjunta el PDF; la ordinaria siguiente excluye al
/// retirado y la prima del semestre sabe lo que ya se pagó aquí. El recaudo corre dentro de la misma
/// unidad de trabajo sin reintento anidado: si Cartera falla, nada queda aprobado.
/// </summary>
public class ApproveSettlementCommandHandlerTests
{
    private static ApproveSettlementCommand Aprobar(Guid runId) => new(runId, Confirm: true);

    [Fact]
    public async Task Aprobar_contabiliza_recauda_en_Cartera_por_obligacion_con_lo_aplicado_cierra_la_ficha_y_adjunta_el_documento()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var persona = await p.D.Db.People.SingleAsync(x => x.Id == p.D.Ana.PersonId);
        persona.IsEmployee = true;
        await p.D.Db.SaveChangesAsync();
        var t = await p.RegistrarAnaAsync();
        (await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, p.Descuento(t, 1001).ObligationPublicId, 1_000_000m, "Refinancia el resto"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        p.Sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success(Guid.NewGuid())));

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Number.Should().StartWith("NM");
        r.Value.PortfolioPayments.Should().HaveCount(2, "dos créditos con valor aplicado; la libranza no pasa por Cartera");
        r.Value.SettlementDocumentAttachmentPublicId.Should().NotBeNull();

        // Cartera recaudó de verdad, una transacción RC por obligación con lo APLICADO (no lo propuesto), a la fecha de retiro.
        var recaudos = await p.D.Db.LendingTransactions.AsNoTracking().Where(x => x.VoucherType == "RC").ToListAsync();
        recaudos.Should().HaveCount(2, "dos créditos con valor aplicado; la libranza no pasa por Cartera");
        var recaudo1001 = recaudos.Single(x => x.PortfolioNumber == 1001);
        recaudo1001.CreditAmount.Should().Be(1_000_000m);
        recaudo1001.TransactionDate.Should().Be(DefinitivaDePrueba.Retiro);
        recaudos.Single(x => x.PortfolioNumber == 1002).CreditAmount.Should().Be(300_000m);
        // Cuatro cuotas de 1001 quedaron en cero y dos siguen vivas; las tres de 1002 se saldaron.
        var cuotas1001 = await p.D.Db.PendingInstallments.AsNoTracking().Where(x => x.PortfolioNumber == 1001).ToListAsync();
        cuotas1001.Count(x => x.BalanceCapital == 0m).Should().Be(4);
        cuotas1001.Sum(x => x.BalanceCapital).Should().Be(500_000m);
        (await p.D.Db.PendingInstallments.AsNoTracking().Where(x => x.PortfolioNumber == 1002).SumAsync(x => x.BalanceCapital)).Should().Be(0m);

        // El comprobante RC lo hizo Cartera por el contrato: la «caja» es la cuenta débito parametrizada de DESC_CARTERA y el
        // crédito va a la cuenta de cartera de la línea, con Ana como tercera; la nómina no duplica ese asiento (D-08).
        var cuentaDescuento = await (from a in p.D.Db.PayrollConceptDefinitionAccounts join c in p.D.Db.ChartOfAccounts on a.DebitAccountId equals c.Id
                                     where a.ConceptCode == WellKnownConceptCodes.LoanDeduction select c).SingleAsync();
        var cuentaCartera = await p.D.Db.ChartOfAccounts.SingleAsync(c => c.Code == DefinitivaDePrueba.CuentaCartera);
        var comprobantesRc = await p.D.Db.AccountingDocuments.AsNoTracking().Include(d => d.VoucherType).Where(d => d.SourceType == "LoanPayment").ToListAsync();
        comprobantesRc.Should().HaveCount(2);
        comprobantesRc.Should().OnlyContain(d => d.VoucherType!.Code == "RC" && d.Date == DefinitivaDePrueba.Retiro);
        var rc1001 = comprobantesRc.Single(d => d.SourcePublicId == recaudo1001.PublicId);
        var lineasRc = await p.D.Db.JournalEntries.AsNoTracking().Where(j => j.DocumentId == rc1001.Id).ToListAsync();
        lineasRc.Single(j => j.Debit > 0m).Should().Match<Domain.Entities.Accounting.Transactions.JournalEntry>(j => j.AccountId == cuentaDescuento.Id && j.Debit == 1_000_000m && j.PersonId == p.D.Ana.PersonId);
        lineasRc.Single(j => j.Credit > 0m).Should().Match<Domain.Entities.Accounting.Transactions.JournalEntry>(j => j.AccountId == cuentaCartera.Id && j.Credit == 1_000_000m && j.PersonId == p.D.Ana.PersonId);
        (await p.D.Db.JournalEntries.AsNoTracking().CountAsync(j => j.AccountId == cuentaDescuento.Id && j.Document!.SourceType == "SettlementRun")).Should().Be(0, "DESC_CARTERA no lleva asiento en el comprobante de la definitiva");

        // El descuento guarda el recaudo y el saldo que quedó; el saldo bajó exactamente lo aplicado.
        var terminacion = await p.D.Db.EmploymentTerminations.Include(x => x.Deductions).SingleAsync(x => x.PublicId == t.TerminationPublicId);
        terminacion.Status.Should().Be(TerminationStatus.Settled);
        terminacion.SettlementDocumentAttachmentPublicId.Should().Be(r.Value.SettlementDocumentAttachmentPublicId);
        terminacion.Deductions.Should().OnlyContain(d => d.Status == SettlementDeductionStatus.Applied);
        var d1001 = terminacion.Deductions.Single(d => d.LoanPortfolioId == p.Credito1001.Id);
        d1001.CarteraTransactionPublicId.Should().Be(recaudo1001.PublicId);
        d1001.RemainingBalanceAfter.Should().Be(500_000m);
        var credito1001 = await p.D.Db.LoanPortfolios.AsNoTracking().SingleAsync(l => l.Id == p.Credito1001.Id);
        credito1001.CurrentBalance.Should().Be(500_000m, "Cartera bajó exactamente lo aplicado");
        credito1001.PaidInstallments.Should().Be(4);
        credito1001.ClosingDate.Should().BeNull();
        (await p.D.Db.LoanPortfolios.AsNoTracking().SingleAsync(l => l.Id == p.Credito1002.Id)).ClosingDate.Should().Be(DefinitivaDePrueba.Retiro, "el crédito 1002 quedó cancelado");
        r.Value.PortfolioPayments.Single(x => x.ObligationPublicId == d1001.PublicId).Remaining.Should().Be(500_000m);

        // La ficha se cerró y la persona dejó de ser empleada.
        var ficha = await p.D.Db.Employees.SingleAsync(e => e.Id == p.D.Ana.Id);
        ficha.Status.Should().Be(-1);
        ficha.TerminationDate.Should().Be(DefinitivaDePrueba.Retiro.ToDateTime(TimeOnly.MinValue));
        ficha.TerminationCause.Should().Be("Despido sin justa causa");
        (await p.D.Db.People.SingleAsync(x => x.Id == p.D.Ana.PersonId)).IsEmployee.Should().BeFalse();

        // La corrida quedó aprobada con su comprobante y las vacaciones pagadas quedaron como movimiento.
        var run = await p.D.Db.PayrollRuns.Include(x => x.AccountingDocument).SingleAsync(x => x.PublicId == t.RunPublicId);
        run.Status.Should().Be(PayrollRunStatus.Approved);
        run.AccountingDocument.Should().NotBeNull();
        run.AccountingDocument!.SourceType.Should().Be("SettlementRun");
        var movimiento = await p.D.Db.VacationMovements.SingleAsync(m => m.EmployeeId == p.D.Ana.Id && m.Kind == VacationMovementKind.SettlementPayout);
        movimiento.Status.Should().Be(VacationMovementStatus.Liquidated);
        movimiento.PayrollRunId.Should().Be(run.Id);
        movimiento.BusinessDays.Should().BePositive();

        await p.D.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollEmployeeTerminated), Arg.Any<CancellationToken>());
        await p.D.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollSettlementApproved), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_Cartera_rechaza_un_recaudo_nada_queda_aprobado()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        // El crédito 1001 tiene saldo pero Cartera no tiene sus cuotas: el recaudo real responde Payment.NoInstallments.
        foreach (var cuota in await p.D.Db.PendingInstallments.Where(x => x.PortfolioNumber == 1001).ToListAsync()) cuota.IsDeleted = true;
        await p.D.Db.SaveChangesAsync();

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payment.NoInstallments");
        r.Error.Message.Should().Contain("Crédito 1001");
        // Sin transacción real (InMemory) el contexto queda con los cambios en memoria, pero nada se guardó como aprobado antes del fallo.
        (await p.D.Db.EmploymentTerminations.AsNoTracking().SingleAsync(x => x.PublicId == t.TerminationPublicId)).Status.Should().Be(TerminationStatus.Registered);
        (await p.D.Db.Employees.AsNoTracking().SingleAsync(e => e.Id == p.D.Ana.Id)).Status.Should().Be(1, "la ficha sigue vigente");
        (await p.D.Db.LendingTransactions.AsNoTracking().CountAsync()).Should().Be(0, "el recaudo no se guarda por su cuenta: va en la transacción de la aprobación");
    }

    [Fact]
    public async Task Si_la_cuenta_del_recaudo_no_esta_habilitada_para_Cartera_la_aprobacion_lo_dice_y_no_recauda()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var cuenta = await (from a in p.D.Db.PayrollConceptDefinitionAccounts join c in p.D.Db.ChartOfAccounts on a.DebitAccountId equals c.Id
                            where a.ConceptCode == WellKnownConceptCodes.LoanDeduction select c).SingleAsync();
        cuenta.EnabledModules &= ~Domain.Enums.Accounting.AccountingModules.Lending;
        await p.D.Db.SaveChangesAsync();
        var t = await p.RegistrarAnaAsync();

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Account.NotEligible");
        r.Error.Message.Should().Contain(cuenta.Code).And.Contain("Crédito 1001");
        (await p.D.Db.LendingTransactions.AsNoTracking().CountAsync()).Should().Be(0);
        (await p.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == t.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    [Fact]
    public async Task Sin_cuentas_para_DESC_CARTERA_la_aprobacion_se_niega_nombrando_el_concepto_y_no_llama_a_Cartera()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var cuentas = await p.D.Db.PayrollConceptDefinitionAccounts.Where(a => a.ConceptCode == WellKnownConceptCodes.LoanDeduction).ToListAsync();
        foreach (var c in cuentas) c.IsDeleted = true;
        await p.D.Db.SaveChangesAsync();
        var t = await p.RegistrarAnaAsync();

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.ConceptAccountsMissing");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { conceptCodes = new[] { WellKnownConceptCodes.LoanDeduction } });
        (await p.D.Db.LendingTransactions.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_ordinaria_siguiente_excluye_al_retirado_y_la_prima_del_semestre_descuenta_la_pagada_aqui()
    {
        var p = new DefinitivaDePrueba(conCartera: false);
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        (await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var primaPagada = (await p.D.Db.PayrollRunLines.Include(l => l.RunEmployee).Where(l => l.RunEmployee!.Run!.PublicId == t.RunPublicId && l.ConceptCode == WellKnownConceptCodes.ServiceBonus).SingleAsync()).Amount;
        primaPagada.Should().BePositive();

        // Octubre: Ana ya no está (FR-005, la exclusión la hace CalculationInputLoader por la terminación Settled).
        var octubre = p.D.Periodo(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), PayPeriodStatus.Open);
        var ordinaria = await p.D.Loader.LoadAsync(octubre, CancellationToken.None);
        ordinaria.Employees.Should().BeEmpty("la única empleada se retiró el 15 de septiembre");

        // La prima 2026-II sabe que ya le pagaron la prima en la definitiva y el motor la deja fuera.
        var prima = await p.D.SettlementLoader.LoadAsync(SettlementLoadRequest.Prima(2026, 2, [p.D.Ana.Id]), CancellationToken.None);
        var input = prima.Employees.Single().Input;
        input.ServiceBonusPaidInSettlements.Should().ContainSingle().Which.Amount.Should().Be(primaPagada);
        var resultado = new SettlementCalculationEngine().Calculate(input);
        resultado.Excluded.Should().BeTrue();
        resultado.ExclusionReasonCode.Should().Be(SettlementReasonCodes.YaPagadaEnDefinitiva);
    }

    [Fact]
    public async Task Sin_confirmacion_o_sobre_otro_tipo_de_corrida_se_rechaza()
    {
        var p = new DefinitivaDePrueba(conCartera: false);
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();

        var sinConfirmar = await p.Aprobar().Handle(new ApproveSettlementCommand(t.RunPublicId, Confirm: false), CancellationToken.None);
        sinConfirmar.Error.Code.Should().Be("Payroll.Settlement.ConfirmationRequired");

        var prima = await LiquidacionDePrueba.PrimaAsync(p.D);
        var otroTipo = await p.Aprobar().Handle(Aprobar(prima.PublicId), CancellationToken.None);
        otroTipo.Error.Code.Should().Be("Payroll.Settlement.KindMismatch");
    }
}
