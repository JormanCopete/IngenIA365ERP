using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Tests.Lending.Payroll;

/// <summary>
/// Feature 013, K2, T0823 (data-model §24.1–§24.10, §32.4; FR-108, FR-113–FR-116): el comportamiento de las entidades de libranza. El
/// convenio se suspende, se reactiva y se cierra sólo sin créditos vigentes; sus versiones rigen por vigencia, no se cruzan y una nueva cierra
/// la anterior la víspera; la regla del no aplicado es una de las tres; el código de respuesta lleva motivo si no descontó; el archivo pasa
/// de generado a reemplazado, enviado o anulado y nada más; el vínculo se traslada conservando la fecha de la libranza.
/// </summary>
public class PayrollAgreementTests
{
    private static DateOnly F(string s) => DateOnly.Parse(s);

    [Fact]
    public void El_convenio_se_suspende_se_reactiva_y_se_cierra_sin_creditos_vigentes()
    {
        var c = new PayrollAgreement { Code = "PAGSED", PaymasterPersonId = 10 };
        c.Status.Should().Be(PayrollAgreementStatus.Active);
        c.Operable.Should().BeTrue();

        c.Suspender();
        c.Operable.Should().BeFalse();
        c.Invoking(x => x.Suspender()).Should().Throw<InvalidOperationException>();
        c.Reactivar();
        c.Status.Should().Be(PayrollAgreementStatus.Active);
        c.Invoking(x => x.Reactivar()).Should().Throw<InvalidOperationException>("sólo uno suspendido se reactiva");

        c.Invoking(x => x.Cerrar(creditosVinculadosVigentes: 3)).Should().Throw<InvalidOperationException>().WithMessage("*3 crédito*");
        c.Cerrar(0);
        c.Status.Should().Be(PayrollAgreementStatus.Closed);
        c.Invoking(x => x.Cerrar(0)).Should().Throw<InvalidOperationException>();
        c.Invoking(x => x.Reactivar()).Should().Throw<InvalidOperationException>("un convenio cerrado no se reactiva");
    }

    [Fact]
    public void La_version_rige_en_su_vigencia_y_la_nueva_cierra_la_anterior_la_vispera()
    {
        var v1 = new PayrollAgreementVersion { ValidFrom = F("2026-01-01") };
        v1.VigenteEn(F("2025-12-31")).Should().BeFalse();
        v1.VigenteEn(F("2026-01-01")).Should().BeTrue();
        v1.VigenteEn(F("2030-01-01")).Should().BeTrue("sin fin");

        v1.CerrarLaVispera(F("2026-07-01"));
        v1.ValidTo.Should().Be(F("2026-06-30"));
        var v2 = new PayrollAgreementVersion { ValidFrom = F("2026-07-01") };

        PayrollAgreementVersion.Vigente([v1, v2], F("2026-06-30")).Should().BeSameAs(v1);
        PayrollAgreementVersion.Vigente([v1, v2], F("2026-07-01")).Should().BeSameAs(v2);
        PayrollAgreementVersion.Vigente([v1, v2], F("2025-01-01")).Should().BeNull("Lending.PayrollAgreement.NoCurrentVersion");
    }

    [Fact]
    public void Las_versiones_no_se_cruzan()
    {
        var v = new PayrollAgreementVersion { ValidFrom = F("2026-01-01"), ValidTo = F("2026-06-30") };
        v.SeCruzaCon(F("2026-06-30"), null).Should().BeTrue();
        v.SeCruzaCon(F("2025-01-01"), F("2026-01-01")).Should().BeTrue();
        v.SeCruzaCon(F("2026-07-01"), null).Should().BeFalse();
        v.SeCruzaCon(F("2025-01-01"), F("2025-12-31")).Should().BeFalse();

        v.Invoking(x => x.CerrarLaVispera(F("2026-01-01"))).Should().Throw<InvalidOperationException>("empezar el mismo día es un cruce");
        v.Invoking(x => x.CerrarLaVispera(F("2026-08-01"))).Should().Throw<InvalidOperationException>("ya terminó antes");
    }

    [Fact]
    public void La_regla_del_no_aplicado_es_una_de_tres_y_la_forma_alterna_es_caja_por_defecto()
    {
        PayrollAgreementVersion.EsReglaDeNoAplicado(NonDeductionFollowUpAction.IncludedInNextFile).Should().BeTrue();
        PayrollAgreementVersion.EsReglaDeNoAplicado(NonDeductionFollowUpAction.MovedToCashPayment).Should().BeTrue();
        PayrollAgreementVersion.EsReglaDeNoAplicado(NonDeductionFollowUpAction.SentToCollection).Should().BeTrue();
        PayrollAgreementVersion.EsReglaDeNoAplicado(NonDeductionFollowUpAction.DebtorNotified).Should().BeFalse();
        PayrollAgreementVersion.EsReglaDeNoAplicado(NonDeductionFollowUpAction.Closed).Should().BeFalse();
        new PayrollAgreementVersion().AlternateRepaymentChannel.Should().Be(RepaymentChannel.Cashier, "FR-116");
    }

    [Fact]
    public void El_codigo_de_respuesta_lleva_motivo_cuando_no_desconto()
    {
        new PayrollAgreementResultCode { Outcome = PayrollResultOutcome.Deducted }.Valido.Should().BeTrue();
        new PayrollAgreementResultCode { Outcome = PayrollResultOutcome.Deducted, NonDeductionReason = NonDeductionReason.Other }.Valido.Should().BeFalse();
        new PayrollAgreementResultCode { Outcome = PayrollResultOutcome.NotDeducted }.Valido.Should().BeFalse();
        new PayrollAgreementResultCode { Outcome = PayrollResultOutcome.PartiallyDeducted, NonDeductionReason = NonDeductionReason.DeductionCap }.Valido.Should().BeTrue();
    }

    [Fact]
    public void El_archivo_va_de_generado_a_reemplazado_enviado_o_anulado_y_nada_mas()
    {
        var instante = new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);
        PayrollDeductionFile Nuevo() => new() { Number = "LD-000001" };

        var reemplazado = Nuevo();
        reemplazado.Reemplazar(nuevoArchivoId: 2, instante);
        reemplazado.Status.Should().Be(PayrollFileStatus.Superseded);
        reemplazado.SupersededByFileId.Should().Be(2);
        reemplazado.Invoking(a => a.MarcarEnviado(instante, Guid.NewGuid(), null)).Should().Throw<InvalidOperationException>(
            "un reemplazado no se envía: el consumo lo rechaza como Lending.PayrollFile.Superseded");

        var enviado = Nuevo();
        var mensaje = Guid.NewGuid();
        enviado.MarcarEnviado(instante, mensaje, " RAD-77 ").Should().BeTrue();
        enviado.Status.Should().Be(PayrollFileStatus.Sent);
        (enviado.SentAt, enviado.SentConfirmationMessagePublicId, enviado.PaymasterReference).Should().Be((instante, mensaje, "RAD-77"));
        enviado.MarcarEnviado(instante.AddHours(1), Guid.NewGuid(), "otro").Should().BeFalse("ya enviado: se procesa sin efecto");
        enviado.SentConfirmationMessagePublicId.Should().Be(mensaje);
        enviado.Invoking(a => a.Reemplazar(3, instante)).Should().Throw<InvalidOperationException>().WithMessage("*ya se envió*",
            "Lending.PayrollFile.AlreadySent");
        enviado.Invoking(a => a.Anular(1, instante, "error")).Should().Throw<InvalidOperationException>("sólo desde Generated");

        var anulado = Nuevo();
        anulado.Invoking(a => a.Anular(1, instante, " ")).Should().Throw<ArgumentException>("el motivo es obligatorio");
        anulado.Anular(1, instante, " archivo con el período equivocado ");
        (anulado.Status, anulado.VoidedByUserId, anulado.VoidReason).Should().Be((PayrollFileStatus.Voided, 1, "archivo con el período equivocado"));

        enviado.AnotarPrimerResultado(instante.AddDays(1));
        enviado.AnotarPrimerResultado(instante.AddDays(2));
        enviado.FirstResultAt.Should().Be(instante.AddDays(1), "el primero queda");
    }

    [Fact]
    public void El_resultado_deja_pendiente_de_giro_lo_descontado_y_sella_la_ventana()
    {
        new PayrollDeductionResult { Outcome = PayrollResultOutcome.Deducted, DeductedAmount = 1m }.DejaPendienteDeGiro.Should().BeTrue();
        new PayrollDeductionResult { Outcome = PayrollResultOutcome.PartiallyDeducted, DeductedAmount = 1m }.DejaPendienteDeGiro.Should().BeTrue();
        new PayrollDeductionResult { Outcome = PayrollResultOutcome.NotDeducted }.DejaPendienteDeGiro.Should().BeFalse();
        PayrollDeductionResult.VencimientoDelGiro(F("2026-12-05"), 3).Should().Be(F("2026-12-08"));
        PayrollDeductionResult.VencimientoDelGiro(null, 3).Should().BeNull();
    }

    [Fact]
    public void El_seguimiento_manual_lleva_motivo_y_actor_y_el_automatico_no_lleva_actor()
    {
        new NonDeductionFollowUp { IsAutomatic = true, Action = NonDeductionFollowUpAction.MovedToCashPayment }.Valido.Should().BeTrue();
        new NonDeductionFollowUp { IsAutomatic = true, ActorUserId = 3 }.Valido.Should().BeFalse();
        new NonDeductionFollowUp { IsAutomatic = false, ActorUserId = 3 }.Valido.Should().BeFalse("una persona da motivo");
        new NonDeductionFollowUp { IsAutomatic = false, ActorUserId = 3, Reason = "Pagará por caja" }.Valido.Should().BeTrue();
    }

    [Fact]
    public void El_traslado_cierra_el_vinculo_y_abre_otro_con_la_misma_fecha_de_la_libranza()
    {
        var vigente = new LoanPayrollLink
        {
            LoanId = 9, RepaymentChannel = RepaymentChannel.PaymasterDeduction, PaymasterPersonId = 100, PayrollAgreementId = 1,
            LibranzaDate = F("2025-03-01"), Origin = LoanPayrollLinkOrigin.Origination, ValidFrom = F("2025-03-01"),
        };

        var nuevo = vigente.Trasladar(pagaduriaId: 200, convenioId: 2, "EMP-4", F("2026-10-01"), "Cambio de empleador");

        (vigente.ValidTo, vigente.EndReason).Should().Be((F("2026-09-30"), "Cambio de empleador"));
        nuevo.Should().BeEquivalentTo(new
        {
            LoanId = 9, RepaymentChannel = RepaymentChannel.PaymasterDeduction, PaymasterPersonId = (int?)200, PayrollAgreementId = (int?)2,
            EmployeeIdAtPaymaster = "EMP-4", LibranzaDate = (DateOnly?)F("2025-03-01"), Origin = LoanPayrollLinkOrigin.Transfer,
            ValidFrom = F("2026-10-01"), OperationId = (long?)null,
        });
        nuevo.ConLibranza.Should().BeTrue();
        vigente.Invoking(v => v.Trasladar(300, 3, null, F("2026-11-01"), "otra vez")).Should().Throw<InvalidOperationException>("ya está cerrado");
    }

    [Fact]
    public void El_retiro_sin_nuevo_empleador_pasa_a_la_forma_alterna_sin_pagaduria()
    {
        var vigente = new LoanPayrollLink
        {
            LoanId = 9, RepaymentChannel = RepaymentChannel.PaymasterDeduction, PaymasterPersonId = 100, PayrollAgreementId = 1,
            LibranzaDate = F("2025-03-01"), ValidFrom = F("2025-03-01"),
        };
        vigente.Invoking(v => v.PasarALaFormaAlterna(RepaymentChannel.OwnPayroll, F("2026-10-01"), "x")).Should().Throw<ArgumentException>();

        var caja = vigente.PasarALaFormaAlterna(RepaymentChannel.Cashier, F("2026-10-01"), "Retiro sin nuevo empleador");

        (caja.RepaymentChannel, caja.PaymasterPersonId, caja.PayrollAgreementId, caja.Origin, caja.ConLibranza)
            .Should().Be((RepaymentChannel.Cashier, (int?)null, (int?)null, LoanPayrollLinkOrigin.ChannelChange, false));
        vigente.ValidTo.Should().Be(F("2026-09-30"));
    }
}
