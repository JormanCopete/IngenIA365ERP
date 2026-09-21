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
/// Feature 010, US3 (FR-020, T065): aprobar la definitiva contabiliza por el ciclo común, aplica
/// cada descuento de Cartera por <c>ProcessPaymentCommand</c> con el valor aplicado, cierra la ficha,
/// apaga la bandera de empleado, deja el movimiento de vacaciones y adjunta el PDF; la ordinaria
/// siguiente excluye al retirado y la prima del semestre sabe lo que ya se pagó aquí.
/// </summary>
public class ApproveSettlementCommandHandlerTests
{
    private static ApproveSettlementCommand Aprobar(Guid runId) => new(runId, Confirm: true);

    [Fact]
    public async Task Aprobar_contabiliza_paga_en_Cartera_por_obligacion_con_lo_aplicado_cierra_la_ficha_y_adjunta_el_documento()
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

        // Cartera recibió un ProcessPaymentCommand por obligación, con lo APLICADO (no lo propuesto), forma NM y la referencia de la liquidación.
        p.PagosEnCartera.Should().HaveCount(2);
        var pago1001 = p.PagosEnCartera.Single(c => c.PortfolioPublicId == p.Credito1001.PublicId);
        pago1001.Amount.Should().Be(1_000_000m);
        pago1001.PaymentMethod.Should().Be(ApproveSettlementCommandHandler.FormaDePagoNomina);
        pago1001.PaymentDate.Should().Be(DefinitivaDePrueba.Retiro);
        pago1001.Reference.Should().Contain(r.Value.Number);
        var cuentaDescuento = await (from a in p.D.Db.PayrollConceptDefinitionAccounts join c in p.D.Db.ChartOfAccounts on a.DebitAccountId equals c.Id
                                     where a.ConceptCode == WellKnownConceptCodes.LoanDeduction select c.Code).SingleAsync();
        pago1001.CashAccountCode.Should().Be(cuentaDescuento, "el recaudo entra por la cuenta débito parametrizada de DESC_CARTERA");
        p.PagosEnCartera.Single(c => c.PortfolioPublicId == p.Credito1002.PublicId).Amount.Should().Be(300_000m);

        // El descuento guarda el recaudo y el saldo que quedó; el saldo bajó exactamente lo aplicado.
        var terminacion = await p.D.Db.EmploymentTerminations.Include(x => x.Deductions).SingleAsync(x => x.PublicId == t.TerminationPublicId);
        terminacion.Status.Should().Be(TerminationStatus.Settled);
        terminacion.SettlementDocumentAttachmentPublicId.Should().Be(r.Value.SettlementDocumentAttachmentPublicId);
        terminacion.Deductions.Should().OnlyContain(d => d.Status == SettlementDeductionStatus.Applied);
        var d1001 = terminacion.Deductions.Single(d => d.LoanPortfolioId == p.Credito1001.Id);
        d1001.CarteraTransactionPublicId.Should().NotBeNull();
        d1001.RemainingBalanceAfter.Should().Be(500_000m);
        (await p.D.Db.LoanPortfolios.SingleAsync(l => l.Id == p.Credito1001.Id)).CurrentBalance.Should().Be(500_000m);
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
        p.Sender.Send(Arg.Any<Application.Lending.Payments.Commands.ProcessPayment.ProcessPaymentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<Application.Lending.Payments.Commands.ProcessPayment.PaymentResultDto>(new Error("Payment.NoInstallments", "No hay cuotas pendientes de pago."))));

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payment.NoInstallments");
        r.Error.Message.Should().Contain("Crédito 1001");
        // Sin transacción real (InMemory) el contexto queda con los cambios en memoria, pero nada se guardó como aprobado antes del fallo.
        (await p.D.Db.EmploymentTerminations.AsNoTracking().SingleAsync(x => x.PublicId == t.TerminationPublicId)).Status.Should().Be(TerminationStatus.Registered);
        (await p.D.Db.Employees.AsNoTracking().SingleAsync(e => e.Id == p.D.Ana.Id)).Status.Should().Be(1, "la ficha sigue vigente");
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
        p.PagosEnCartera.Should().BeEmpty();
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
