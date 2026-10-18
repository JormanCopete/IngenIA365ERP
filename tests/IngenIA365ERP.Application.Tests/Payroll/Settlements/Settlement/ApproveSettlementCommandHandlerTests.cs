using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Policies;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;

/// <summary>
/// Feature 010, US3 (FR-020, T065; research R14): aprobar la definitiva contabiliza por el ciclo común, cierra la ficha,
/// apaga la bandera de empleado, deja el movimiento de vacaciones y adjunta el PDF; la ordinaria siguiente excluye al
/// retirado y la prima del semestre sabe lo que ya se pagó aquí.
/// <para>
/// Feature 013 (T0107; T92, R104, R109): los descuentos de Cartera se aplican por el contrato de obligaciones
/// (<c>ContratoDeObligacionesDePrueba</c>) en el gancho <c>antesDeContabilizar</c>: se vuelve a consultar (conjunto distinto →
/// <c>LoanProposalOutdated</c>), el lote va <c>NonInteractive</c>, recaudador <c>NOM</c>/<c>Payroll</c>, clase
/// <c>OwnPayrollDeduction</c>, pagadora Ana, fecha valor = aprobación, clave <c>NOM:{run:N}:{obligación:N}</c> en orden
/// ascendente, todo o nada (<c>LoanAmountsChanged</c>), y con «Cartera por activar» exige el reconocimiento.
/// </para>
/// </summary>
public class ApproveSettlementCommandHandlerTests
{
    private static ApproveSettlementCommand Aprobar(Guid runId, bool reconocer = false) => new(runId, Confirm: true, AcknowledgeLendingPendingActivation: reconocer);

    [Fact]
    public async Task Aprobar_aplica_el_lote_por_el_contrato_guarda_comprobante_y_saldo_contabiliza_DESC_CARTERA_y_cierra_la_ficha()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var persona = await p.D.Db.People.SingleAsync(x => x.Id == p.D.Ana.PersonId);
        persona.IsEmployee = true;
        await p.D.Db.SaveChangesAsync();
        var t = await p.RegistrarAnaAsync();
        (await p.Ajustar().Handle(new AdjustSettlementDeductionCommand(t.RunPublicId, p.Descuento(t, "1001").ObligationPublicId, 1_000_000m, "Refinancia el resto"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        p.Contrato.ComoRespondeAplicar[p.Credito1001.ObligationPublicId] = new(ResultadoDelContrato.Applied, SaldoQueQueda: 500_000m);
        p.Sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success(Guid.NewGuid())));

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Number.Should().StartWith("NM");
        r.Value.PortfolioPayments.Should().HaveCount(2, "dos créditos con valor aplicado; la libranza no pasa por Cartera");
        r.Value.SettlementDocumentAttachmentPublicId.Should().NotBeNull();

        // El lote: uno solo, sin interacción, de la nómina, en orden ascendente de obligación.
        var lote = p.Contrato.Aplicaciones.Should().ContainSingle().Subject;
        lote.Collector.Should().Be(new Recaudador("NOM", ClaseDeRecaudador.Payroll));
        lote.Form.Should().Be(FormaDeAplicacion.NonInteractive);
        lote.NotDeducted.Should().BeEmpty();
        lote.Payments.Select(x => x.ObligationPublicId).Should().BeInAscendingOrder();
        lote.Payments.Should().OnlyContain(x => x.MeansClass == ClaseDeMedioDeRecaudo.OwnPayrollDeduction && x.PayerPublicId == p.PersonaDeAna
                                                && x.ValueDate == new DateOnly(2026, 9, 20) && x.SimulationId == null
                                                && x.IdempotencyKey == $"NOM:{t.RunPublicId:N}:{x.ObligationPublicId:N}");
        var pago1001 = lote.Payments.Single(x => x.ObligationPublicId == p.Credito1001.ObligationPublicId);
        pago1001.Modality.Should().Be(ModalidadDePago.FreeAmount, "se ajustó");
        pago1001.Amount.Should().Be(1_000_000m, "lo APLICADO, no lo propuesto");
        var pago1002 = lote.Payments.Single(x => x.ObligationPublicId == p.Credito1002.ObligationPublicId);
        pago1002.Modality.Should().Be(ModalidadDePago.FullPayoff, "política SaldoTotal sin ajuste");
        pago1002.Amount.Should().Be(300_000m);

        // El descuento guarda el comprobante de Cartera y el saldo que quedó.
        var terminacion = await p.D.Db.EmploymentTerminations.Include(x => x.Deductions).SingleAsync(x => x.PublicId == t.TerminationPublicId);
        terminacion.Status.Should().Be(TerminationStatus.Settled);
        terminacion.SettlementDocumentAttachmentPublicId.Should().Be(r.Value.SettlementDocumentAttachmentPublicId);
        terminacion.Deductions.Should().OnlyContain(d => d.Status == SettlementDeductionStatus.Applied);
        var d1001 = terminacion.Deductions.Single(d => d.ObligationPublicId == p.Credito1001.ObligationPublicId);
        d1001.ObligationNumber.Should().Be("1001");
        d1001.CarteraTransactionPublicId.Should().NotBeNull().And.Be(r.Value.PortfolioPayments.Single(x => x.ObligationPublicId == d1001.PublicId).PaymentPublicId);
        d1001.RemainingBalanceAfter.Should().Be(500_000m);
        r.Value.PortfolioPayments.Single(x => x.ObligationPublicId == d1001.PublicId).Remaining.Should().Be(500_000m);

        // Enmienda D-08: DESC_CARTERA contabiliza en el comprobante de la definitiva contra la cuenta del concepto.
        var cuentaDescuento = await (from a in p.D.Db.PayrollConceptDefinitionAccounts join c in p.D.Db.ChartOfAccounts on a.CreditAccountId equals c.Id
                                     where a.ConceptCode == WellKnownConceptCodes.LoanDeduction select c).SingleAsync();
        (await p.D.Db.JournalEntries.AsNoTracking().Where(j => j.AccountId == cuentaDescuento.Id && j.Document!.SourceType == "SettlementRun").SumAsync(j => j.Credit + j.Debit))
            .Should().Be(1_300_000m, "lo aplicado de los dos créditos va a la cuenta de DESC_CARTERA");

        // La ficha se cerró y la persona dejó de ser empleada.
        var ficha = await p.D.Db.Employees.SingleAsync(e => e.Id == p.D.Ana.Id);
        ficha.Status.Should().Be(-1);
        ficha.TerminationDate.Should().Be(DefinitivaDePrueba.Retiro.ToDateTime(TimeOnly.MinValue));
        ficha.TerminationCause.Should().Be("Despido sin justa causa");
        (await p.D.Db.People.SingleAsync(x => x.Id == p.D.Ana.PersonId)).IsEmployee.Should().BeFalse();

        var run = await p.D.Db.PayrollRuns.Include(x => x.AccountingDocument).SingleAsync(x => x.PublicId == t.RunPublicId);
        run.Status.Should().Be(PayrollRunStatus.Approved);
        run.AccountingDocument!.SourceType.Should().Be("SettlementRun");
        run.LendingActivationAckBy.Should().BeNull("nadie estaba en una carga por activar");
        var movimiento = await p.D.Db.VacationMovements.SingleAsync(m => m.EmployeeId == p.D.Ana.Id && m.Kind == VacationMovementKind.SettlementPayout);
        movimiento.Status.Should().Be(VacationMovementStatus.Liquidated);
        movimiento.PayrollRunId.Should().Be(run.Id);

        await p.D.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollEmployeeTerminated), Arg.Any<CancellationToken>());
        await p.D.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollSettlementApproved), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Politica_SoloCuotasCausadas_aplica_la_cuota_del_ciclo()
    {
        var p = new DefinitivaDePrueba();
        p.D.Politica(CompanyPolicyKeys.DeduccionAlRetiroModo, CompanyPolicyKeys.DeduccionAlRetiroModoValores.SoloCuotasCausadas, new DateOnly(2026, 1, 1));
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        p.Descuento(t, "1001").Proposed.Should().Be(250_000m, "SoloCuotasCausadas propone lo exigible (DueAmount)");

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        p.Contrato.Aplicaciones.Single().Payments.Should().OnlyContain(x => x.Modality == ModalidadDePago.CycleInstallment);
    }

    [Fact]
    public async Task Un_item_rechazado_responde_LoanAmountsChanged_con_los_valores_de_Cartera_y_nada_queda_aprobado()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        p.Contrato.ComoRespondeAplicar[p.Credito1001.ObligationPublicId] = ContratoDeObligacionesDePrueba.Rechazo();

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Settlement.LoanAmountsChanged");
        r.Error.Message.Should().Contain("1001").And.Contain("ya no recibe pagos");
        r.Error.Should().BeOfType<ErrorConDatos>();
        (await p.D.Db.EmploymentTerminations.AsNoTracking().SingleAsync(x => x.PublicId == t.TerminationPublicId)).Status.Should().Be(TerminationStatus.Registered);
        (await p.D.Db.Employees.AsNoTracking().SingleAsync(e => e.Id == p.D.Ana.Id)).Status.Should().Be(1, "la ficha sigue vigente");
        (await p.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == t.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft);
        (await p.D.Db.SettlementDeductions.AsNoTracking().Where(d => d.Termination!.PublicId == t.TerminationPublicId).ToListAsync())
            .Should().OnlyContain(d => d.CarteraTransactionPublicId == null, "todo o nada: no se guardó ningún comprobante");
    }

    [Fact]
    public async Task Un_descuento_que_Contabilidad_no_puede_registrar_responde_LoanDeductionNotPostable_y_nada_queda_aprobado()
    {
        // Feature 013, T0801 (A8 = «Bloquear todo», 2026-10-14): el rechazo por la matriz contable tiene su propio código y dice qué regla falta.
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        p.Contrato.ComoRespondeAplicar[p.Credito1001.ObligationPublicId] = ContratoDeObligacionesDePrueba.Rechazo(RecaudoPorNomina.CodigoNoContabilizable,
            "Contabilidad no tiene cómo registrar PagoAplicado en el rol RecaudoPorAplicar. Pida a la contadora completar la matriz de Cartera.");

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.LoanDeductionNotPostable");
        r.Error.Message.Should().Contain("1001").And.Contain("RecaudoPorAplicar");
        (await p.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == t.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft);
        (await p.D.Db.EmploymentTerminations.AsNoTracking().SingleAsync(x => x.PublicId == t.TerminationPublicId)).Status.Should().Be(TerminationStatus.Registered);
    }

    [Fact]
    public async Task Un_excedente_tambien_responde_LoanAmountsChanged()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        p.Contrato.ComoRespondeAplicar[p.Credito1002.ObligationPublicId] = new(ResultadoDelContrato.Applied, Excedente: 20_000m);

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.LoanAmountsChanged");
        r.Error.Message.Should().Contain("1002").And.Contain("280.000");
        (await p.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == t.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    [Fact]
    public async Task Si_las_obligaciones_vivas_cambiaron_desde_el_calculo_responde_LoanProposalOutdated_sin_llamar_al_contrato()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        p.Obligaciones.Agregar(p.PersonaDeAna, ConsultaDeObligacionesDePrueba.Obligacion("1003", 80_000m, exigible: 80_000m));

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.LoanProposalOutdated");
        r.Error.Message.Should().Contain("Recalcule");
        p.Contrato.Aplicaciones.Should().BeEmpty();
        (await p.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == t.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    [Fact]
    public async Task Con_la_persona_migrada_sin_activar_exige_reconocerlo_y_lo_guarda_en_la_corrida()
    {
        var p = new DefinitivaDePrueba(conCartera: false);
        p.ConContabilidad();
        p.Obligaciones.MarcarMigrada(p.PersonaDeAna);
        var t = await p.RegistrarAnaAsync();
        t.Warnings.Should().Contain(w => w.Code == "Payroll.LendingPendingActivation");

        var sinReconocer = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);
        sinReconocer.Error.Code.Should().Be("Payroll.LendingPendingActivationAckRequired");
        sinReconocer.Error.Message.Should().Contain("Ana");

        var reconocida = await p.Aprobar().Handle(Aprobar(t.RunPublicId, reconocer: true), CancellationToken.None);
        reconocida.IsSuccess.Should().BeTrue(reconocida.Error.Message);
        var run = await p.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == t.RunPublicId);
        run.LendingActivationAckBy.Should().Be(DefinitivaDePrueba.Contadora.UserName);
        run.LendingActivationAckAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Sin_cuentas_para_DESC_CARTERA_la_aprobacion_se_niega_nombrando_el_concepto()
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
        (await p.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == t.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft);
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

    /// <summary>
    /// D-29: la definitiva paga el último tramo (SALARIO_PENDIENTE) y la ordinaria del período donde cae el
    /// retiro ya no incluye al empleado. Si esa ordinaria ya estaba calculada con el empleado, aprobar la
    /// definitiva la deja Stale y al recargarla el empleado no entra. Hasta la revisión de N1 el borrador
    /// seguía vivo con los mismos días y se aprobaba pagándolos dos veces.
    /// </summary>
    [Fact]
    public async Task Aprobar_deja_Stale_la_ordinaria_calculada_del_periodo_del_retiro_y_la_retirada_ya_no_entra_en_ella()
    {
        var p = new DefinitivaDePrueba(conCartera: false);
        p.ConContabilidad();
        // Septiembre ya calculado con Ana, antes de registrar el retiro del 15.
        var ordinaria = new CalculatePayrollRunCommandHandler(p.D.Db, p.D.Loader, p.D.Recurrentes, p.D.CuotasDeCartera, p.D.Lock, p.D.Clock, p.D.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);
        var calculada = await ordinaria.Handle(new CalculatePayrollRunCommand(p.Septiembre.PublicId), CancellationToken.None);
        calculada.IsSuccess.Should().BeTrue(calculada.Error.Message);
        (await p.D.Db.PayrollRunEmployees.CountAsync(e => e.Run!.PublicId == calculada.Value.RunPublicId && e.EmployeeId == p.D.Ana.Id)).Should().Be(1);
        var t = await p.RegistrarAnaAsync();
        (await p.D.Db.PayrollRunLines.AnyAsync(l => l.RunEmployee!.Run!.PublicId == t.RunPublicId && l.ConceptCode == WellKnownConceptCodes.PendingSalary && l.Amount > 0m))
            .Should().BeTrue("la definitiva paga los días 1 a 15 de septiembre");

        var r = await p.Aprobar().Handle(Aprobar(t.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await p.D.Db.PayrollRuns.SingleAsync(x => x.PublicId == calculada.Value.RunPublicId)).Status.Should().Be(PayrollRunStatus.Stale,
            "el borrador de septiembre traía a Ana con el mes entero y la definitiva acaba de pagar su último tramo");
        var septiembre = await p.D.Loader.LoadAsync(await p.D.Db.PayPeriods.SingleAsync(x => x.Id == p.Septiembre.Id), CancellationToken.None);
        septiembre.Employees.Should().BeEmpty("la definitiva aprobada con retiro dentro de septiembre saca a Ana de la ordinaria de septiembre (D-29)");
    }

    /// <summary>
    /// D-29 en el orden inverso: si la ordinaria del período se aprueba con la definitiva todavía en borrador,
    /// la ordinaria pagó el tramo; la definitiva queda Stale y al recalcularla sale sin salario pendiente.
    /// </summary>
    [Fact]
    public async Task Aprobar_la_ordinaria_del_periodo_del_retiro_deja_Stale_la_definitiva_en_borrador_que_al_recalcular_sale_sin_salario_pendiente()
    {
        var p = new DefinitivaDePrueba(conCartera: false);
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        var ordinaria = new CalculatePayrollRunCommandHandler(p.D.Db, p.D.Loader, p.D.Recurrentes, p.D.CuotasDeCartera, p.D.Lock, p.D.Clock, p.D.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);
        var calculada = await ordinaria.Handle(new CalculatePayrollRunCommand(p.Septiembre.PublicId), CancellationToken.None);
        calculada.IsSuccess.Should().BeTrue(calculada.Error.Message);
        var audit = new PayrollAuditEmitter(p.D.Audit, DefinitivaDePrueba.Contadora, p.D.Clock, NullLogger<PayrollAuditEmitter>.Instance);
        var aprobador = new ApprovePayrollRunCommandHandler(p.D.Db, p.D.Contabilizador(DefinitivaDePrueba.Contadora), p.D.Policies, p.D.Permissions, p.D.Clock, DefinitivaDePrueba.Contadora, audit, p.D.StaleMarker, p.D.Recaudo, p.D.Obligaciones);

        var aprobada = await aprobador.Handle(new ApprovePayrollRunCommand(calculada.Value.RunPublicId, Confirm: true), CancellationToken.None);

        aprobada.IsSuccess.Should().BeTrue(aprobada.Error.Message);
        (await p.D.Db.PayrollRuns.SingleAsync(x => x.PublicId == t.RunPublicId)).Status.Should().Be(PayrollRunStatus.Stale, "la ordinaria de septiembre acaba de pagar el tramo que la definitiva traía");
        var v2 = await p.Recalcular().Handle(new RecalculateSettlementCommand(t.RunPublicId), CancellationToken.None);
        v2.IsSuccess.Should().BeTrue(v2.Error.Message);
        (await p.D.Db.PayrollRunLines.AnyAsync(l => l.RunEmployee!.Run!.PublicId == v2.Value.RunPublicId && l.ConceptCode == WellKnownConceptCodes.PendingSalary))
            .Should().BeFalse("septiembre ya está aprobado: la última nómina pagó el salario y la definitiva no lo repite");
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
