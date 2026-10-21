using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 013, US3, T0417 (FR-042, FR-045, D12; data-model §7.2): cada requisito incumplido responde su código y no escribe nada —aprobación
/// vencida, garantías pendientes, codeudores insuficientes, usura del día, concentración, sin patrimonio técnico, bloqueos, listas, segregación,
/// usuario sin persona, monto operativo, oficina sin sucursal contable, lo aprobado, tramos, cuenta del beneficiario, valores nacionales—; en
/// producción un pagaré simulado no perfecciona; la validación previa bloquea sólo con <c>Bloquear</c>. El primer tramo hace nacer el crédito con
/// sus condiciones y su plan sellados, sus partes, su vínculo de forma de pago y su preferencia; el segundo amplía el crédito y regenera el plan.
/// </summary>
public sealed class ExecuteLoanDisbursementCommandHandlerTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DesembolsoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private async Task NoEscribioNadaAsync()
    {
        (await _d.Db.Loans.CountAsync()).Should().Be(0);
        (await _d.Db.LoanOperations.CountAsync()).Should().Be(0);
        (await _d.Db.DisbursementOrders.CountAsync()).Should().Be(0);
    }

    private async Task<Error> FallaAsync(DisbursementInput entrada)
    {
        var r = await _d.DesembolsarAsync(entrada);
        r.IsFailure.Should().BeTrue("el requisito incumplido detiene el desembolso");
        await NoEscribioNadaAsync();
        return r.Error;
    }

    // -------------------------------------------------------------------------------------------------------- requisitos --

    [Fact]
    public async Task La_aprobacion_vencida_responde_ApprovalExpired_con_la_fecha()
    {
        var s = await _d.AprobadaAsync();
        var hasta = (await _d.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId)).ApprovalValidUntil!.Value;
        _d.G.HoyLocal = hasta.AddDays(1);

        var e = await FallaAsync(_d.Entrada(s));

        e.Code.Should().Be("Lending.Application.ApprovalExpired");
        DesembolsoDePrueba.Datos(e).GetProperty("expiredAt").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Sin_el_pagare_que_exige_la_regla_responde_GuaranteesPending_con_lo_pendiente()
    {
        var s = await _d.AprobadaAsync();
        await _d.VersionDesdeMananaAsync(new CollateralRuleInput(AmountFrom: 1m, PromissoryNoteKind: PromissoryNoteKind.Physical));

        var e = await FallaAsync(_d.Entrada(s));

        e.Code.Should().Be(ErroresDeDesembolso.CodigoGarantiasPendientes);
        var pendiente = DesembolsoDePrueba.Datos(e).GetProperty("pending")[0];
        pendiente.GetProperty("kind").GetString().Should().Be("PromissoryNote");
        pendiente.GetProperty("reason").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Menos_codeudores_de_los_que_pide_la_regla_responde_CoDebtorsInsufficient()
    {
        var s = await _d.AprobadaAsync(_d.O.Solicitud(codeudores: _d.O.Beto));
        await _d.VersionDesdeMananaAsync(new CollateralRuleInput(AmountFrom: 1m, AmountTo: 30_000_000m, TermFrom: 1, CoDebtors: 2));

        var e = await FallaAsync(_d.Entrada(s));

        e.Code.Should().Be("Lending.Collateral.CoDebtorsInsufficient");
        var datos = DesembolsoDePrueba.Datos(e);
        datos.GetProperty("required").GetInt32().Should().Be(2);
        datos.GetProperty("present").GetInt32().Should().Be(1);
        datos.GetProperty("ruleOrder").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task En_produccion_un_pagare_simulado_no_perfecciona_y_dice_que_configurar()
    {
        var s = await _d.AprobadaAsync();
        await _d.VersionDesdeMananaAsync(new CollateralRuleInput(AmountFrom: 1m, PromissoryNoteKind: PromissoryNoteKind.Dematerialized));
        var solicitud = await _d.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId);
        _d.Db.PromissoryNotes.Add(new PromissoryNote
        {
            Kind = PromissoryNoteKind.Dematerialized, ApplicationId = solicitud.Id, IssuedOn = DesembolsoDePrueba.Hoy, Status = PromissoryNoteStatus.InCustody,
            CustodianChannelCode = "SIMULADO", ExternalNoteId = "SIM-1", IsSimulated = true, CreatedBy = "test",
        });
        await _d.Db.SaveChangesAsync();
        _d.O.Opciones.AmbienteDePruebas = false;

        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Lending.Adapter.SimulatedInProduction");

        _d.O.Opciones.AmbienteDePruebas = true;
        (await _d.DesembolsarAsync(_d.Entrada(s))).IsSuccess.Should().BeTrue("fuera de producción el simulado sí perfecciona");
    }

    [Fact]
    public async Task La_usura_del_dia_del_desembolso_menor_que_la_de_la_aprobacion_responde_UsuryExceeded()
    {
        var s = await _d.AprobadaAsync();
        await _d.O.L.UsuraAsync(Configuration.LineasDePrueba.ClaveConsumo, 0.15m, DesembolsoDePrueba.Hoy.AddDays(1));
        _d.Manana();

        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Lending.Usury.Exceeded");
    }

    [Fact]
    public async Task Sin_la_usura_del_dia_publicada_responde_NationalValuesMissing()
    {
        using var d = await DesembolsoDePrueba.CrearAsync(usuraHasta: DesembolsoDePrueba.Hoy);
        var s = await d.AprobadaAsync();
        d.Manana();

        var r = await d.DesembolsarAsync(d.Entrada(s));

        r.Error.Code.Should().Be("NationalValues.Missing");
    }

    [Fact]
    public async Task La_concentracion_excedida_y_sin_patrimonio_tecnico_detienen_el_desembolso()
    {
        var s = await _d.AprobadaAsync();
        _d.O.PatrimonioTecnico = new IndicadorRegistradoDto(Guid.NewGuid(), 10_000_000m, "COP", new DateOnly(2026, 11, 30), "Contabilidad");
        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Lending.Concentration.LimitExceeded");

        _d.O.PatrimonioTecnico = null;
        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Lending.Concentration.TechnicalCapitalUnavailable");
    }

    [Fact]
    public async Task Un_bloqueo_vigente_responde_DebtorBlocked()
    {
        var s = await _d.AprobadaAsync();
        _d.O.BloqueosVigentes.Add(new DebtorBlock { PersonId = _d.O.Ana.Id, Reason = DebtorBlockReason.Manual, StartedOn = DesembolsoDePrueba.Hoy, SourceKey = "x" });

        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Lending.Debtor.Blocked");
    }

    [Fact]
    public async Task Una_coincidencia_en_listas_sin_liberar_responde_RestrictiveListMatch()
    {
        var s = await _d.AprobadaAsync();
        var solicitud = await _d.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId);
        _d.Db.ScreeningResults.Add(new ScreeningResult
        {
            PersonId = _d.O.Ana.Id, Context = ScreeningContext.Application, ApplicationId = solicitud.Id, ChannelCode = "CARGA-PROPIA", ListVersions = "[]",
            Outcome = Domain.Enums.Lending.ScreeningOutcome.Match, QueriedAt = _d.G.UtcNow.AddHours(1),
        });
        await _d.Db.SaveChangesAsync();

        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Compliance.RestrictiveList.Match");
    }

    [Fact]
    public async Task Quien_decidio_no_desembolsa_y_un_usuario_sin_persona_tampoco()
    {
        var s = await _d.AprobadaAsync();
        _d.G.ActuarComo(_d.O.Analista);
        var e = await FallaAsync(_d.Entrada(s));
        e.Code.Should().Be("Lending.Segregation.Conflict");
        DesembolsoDePrueba.Datos(e).GetProperty("previousAction").GetString().Should().Be(AccionesDeSegregacion.Decidir);

        _d.G.ActuarComo(_d.O.SinPersona);
        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Lending.Security.UserWithoutPerson");
    }

    [Fact]
    public async Task Sobre_el_monto_operativo_de_Disbursements_Execute_responde_AmountLimitExceeded()
    {
        var s = await _d.AprobadaAsync();
        _d.MontoOperativo(1_000_000m);

        var e = await FallaAsync(_d.Entrada(s));

        e.Code.Should().Be("Lending.AmountLimit.Exceeded");
        DesembolsoDePrueba.Datos(e).GetProperty("limit").GetDecimal().Should().Be(1_000_000m);
    }

    [Fact]
    public async Task Una_oficina_sin_sucursal_contable_responde_AccountingBranchMissing()
    {
        var s = await _d.AprobadaAsync();
        var norte = await _d.Db.Branches.SingleAsync(b => b.Id == _d.O.Norte.Id);
        norte.IsDeleted = true;
        await _d.Db.SaveChangesAsync();

        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Lending.Branch.AccountingBranchMissing");
    }

    [Fact]
    public async Task Mas_de_lo_aprobado_y_un_tramo_en_una_linea_sin_tramos_se_rechazan()
    {
        var s = await _d.AprobadaAsync();

        var e = await FallaAsync(_d.Entrada(s) with { TrancheAmount = 5_000_000m });
        e.Code.Should().Be("Lending.Disbursement.ExceedsApproved");
        var datos = DesembolsoDePrueba.Datos(e);
        datos.GetProperty("approved").GetDecimal().Should().Be(3_000_000m);
        datos.GetProperty("disbursed").GetDecimal().Should().Be(0m);
        datos.GetProperty("requested").GetDecimal().Should().Be(5_000_000m);

        (await FallaAsync(_d.Entrada(s) with { TrancheAmount = 1_000_000m })).Code.Should().Be("Lending.Disbursement.TrancheNotAllowed");
    }

    [Fact]
    public async Task Una_transferencia_sin_cuenta_del_beneficiario_responde_BeneficiaryAccountMissing()
    {
        var s = await _d.AprobadaAsync();

        var e = await FallaAsync(_d.Entrada(s) with { Beneficiary = new BeneficiaryInput(_d.O.Ana.PublicId) });

        e.Code.Should().Be("Lending.Disbursement.BeneficiaryAccountMissing");
    }

    // ------------------------------------------------------------------------------------------- validación previa (T56) --

    private void SinRegla() =>
        _d.Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(false,
                [new HallazgoDeCarteraDto("CreditoDesembolsado", "Principal", null, null, "RuleMissing", "Falta la regla", new QuienCorrigeDto("Accounting", null, null))], [])));

    [Fact]
    public async Task Sin_regla_y_con_Bloquear_responde_NotPostable()
    {
        var s = await _d.AprobadaAsync();
        SinRegla();
        _d.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.ContabilidadFaltaDeRegla, ScopeKind = ParameterScopeKind.None, Value = "Bloquear",
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedAt = _d.G.UtcNow.AddDays(-10),
        });
        await _d.Db.SaveChangesAsync();

        (await FallaAsync(_d.Entrada(s))).Code.Should().Be("Lending.Prevalidation.NotPostable");
    }

    [Fact]
    public async Task Sin_regla_con_Advertir_sale_en_avisos_y_se_sella_NotPostable()
    {
        var s = await _d.AprobadaAsync();
        SinRegla();
        // El defecto es «Bloquear» (decisión del dueño, 2026-10-14): «Advertir» se fija a propósito.
        _d.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.ContabilidadFaltaDeRegla, ScopeKind = ParameterScopeKind.None, Value = "Advertir",
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedAt = _d.G.UtcNow.AddDays(-10),
        });
        await _d.Db.SaveChangesAsync();

        var r = await _d.DesembolsadoAsync(_d.Entrada(s));

        r.Warnings.Should().NotBeEmpty();
        r.Prevalidation.Postable.Should().BeFalse();
        (await _d.Db.LoanOperations.SingleAsync()).PrevalidationOutcome.Should().Be("NotPostable");
    }

    [Fact]
    public async Task Sin_respuesta_de_Contabilidad_sigue_con_NoResponse()
    {
        var s = await _d.AprobadaAsync();
        _d.Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("sin red"));

        var r = await _d.DesembolsadoAsync(_d.Entrada(s));

        r.Prevalidation.Responded.Should().BeFalse();
        (await _d.Db.LoanOperations.SingleAsync()).PrevalidationOutcome.Should().Be("NoResponse");
    }

    // --------------------------------------------------------------------------------------------------- primer tramo --

    [Fact]
    public async Task El_primer_tramo_hace_nacer_el_credito_con_condiciones_y_plan_sellados()
    {
        var s = await _d.AprobadaAsync(_d.O.Solicitud(codeudores: _d.O.Beto));

        var r = await _d.DesembolsadoAsync(_d.Entrada(s));

        var credito = await _d.Db.Loans.SingleAsync();
        credito.Status.Should().Be(LoanStatus.Active);
        credito.Origin.Should().Be(LoanOrigin.Origination);
        credito.Number.Should().Be(r.LoanNumber).And.StartWith("CR-");
        credito.OpenedOn.Should().Be(DesembolsoDePrueba.Hoy);
        (await _d.Db.LoanControls.SingleAsync()).LastAccruedDate.Should().Be(DesembolsoDePrueba.Hoy);

        var condiciones = await _d.Db.LoanTermsVersions.SingleAsync();
        condiciones.Cause.Should().Be(TermsVersionCause.Origination);
        condiciones.RulesVersion.Should().NotBeNullOrEmpty();
        condiciones.ResultingEffectiveRate.Should().NotBeNull();
        condiciones.ChargesSpread.Should().NotBeNull();
        condiciones.UsuryValuePublicId.Should().NotBeNull();
        condiciones.DefaultOnInterestAllowed.Should().BeFalse("la ley no permite mora sobre intereses (límite 0)");

        var plan = await _d.Db.RepaymentScheduleVersions.SingleAsync();
        plan.VersionNumber.Should().Be(1);
        plan.PrincipalBase.Should().Be(3_000_000m);
        (await _d.Db.RepaymentInstallments.SumAsync(c => c.Principal)).Should().Be(3_000_000m);

        var parte = await _d.Db.LoanParties.SingleAsync();
        (parte.PersonId, parte.Role).Should().Be((_d.O.Beto.Id, CreditPartyRole.CoDebtor));
        var vinculo = await _d.Db.LoanPayrollLinks.SingleAsync();
        (vinculo.Origin, vinculo.RepaymentChannel, vinculo.ValidTo).Should().Be((LoanPayrollLinkOrigin.Origination, RepaymentChannel.Cashier, (DateOnly?)null));
        (await _d.Db.PrepaymentPreferences.SingleAsync(p => p.LoanId == credito.Id)).Origin.Should().Be(PreferenceOrigin.Application);

        var solicitud = await _d.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        (solicitud.Status, solicitud.LoanId).Should().Be((CreditApplicationStatus.Disbursed, credito.Id));
    }

    [Fact]
    public async Task La_operacion_lleva_numero_secuencia_fecha_contable_y_entradas_canonicas()
    {
        var s = await _d.AprobadaAsync();

        var r = await _d.DesembolsadoAsync(_d.Entrada(s));

        var op = await _d.Db.LoanOperations.SingleAsync();
        (op.Kind, op.Number, op.Sequence, op.TrancheNumber, op.DisbursementForm).Should()
            .Be((LoanOperationKind.Disbursement, r.OperationNumber, 1, (int?)1, (DisbursementForm?)DisbursementForm.Transfer));
        op.Number.Should().StartWith("DS-");
        op.AccountingDate.Should().Be(DesembolsoDePrueba.Hoy);
        op.ActorUserId.Should().Be(_d.Cajera.Id);
        op.Payload.Should().Contain("\"tranche\":1");
        op.InputsSha256.Should().HaveLength(64);
        var fila = await _d.Db.LoanTransactions.SingleAsync();
        (fila.Kind, fila.Concept, fila.Amount, fila.OperationId).Should().Be((LoanTransactionKind.Disbursement, LoanConcept.Principal, 3_000_000m, (long?)op.Id));
    }

    [Fact]
    public async Task Registra_la_intervencion_Desembolsar_y_audita_la_distribucion()
    {
        var s = await _d.AprobadaAsync();

        await _d.DesembolsadoAsync(_d.Entrada(s));

        (await _d.Db.CaseParticipations.Where(c => c.CaseRootPublicId == s.PublicId).Select(c => c.Action).ToListAsync())
            .Should().Contain(AccionesDeSegregacion.Desembolsar);
        (await _d.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingOperationRegistered);
    }

    // ----------------------------------------------------------------------------------------------------- por tramos --

    [Fact]
    public async Task El_segundo_tramo_no_crea_credito_y_regenera_el_plan_sobre_lo_desembolsado()
    {
        using var d = await DesembolsoDePrueba.CrearAsync(OriginacionDeTramos());
        var s = await d.AprobadaAsync();
        var primero = await d.DesembolsadoAsync(d.Entrada(s) with { TrancheAmount = 2_000_000m });

        var segundo = await d.DesembolsadoAsync(d.Entrada(s) with { LoanPublicId = primero.LoanPublicId, TrancheAmount = 1_000_000m });

        (await d.Db.Loans.CountAsync()).Should().Be(1);
        segundo.Tranche.Should().Be(2);
        segundo.ScheduleVersion.Should().Be(2);
        (await d.Db.RepaymentScheduleVersions.SingleAsync(v => v.VersionNumber == 2)).PrincipalBase.Should().Be(3_000_000m);
        (await d.Db.LoanOperations.Select(o => o.TrancheNumber).ToListAsync()).Should().BeEquivalentTo(new int?[] { 1, 2 });
        (await d.Db.LoanControls.SingleAsync()).Sequence.Should().Be(2);

        (await d.DesembolsarAsync(d.Entrada(s) with { LoanPublicId = primero.LoanPublicId, TrancheAmount = 1m })).Error.Code
            .Should().Be("Lending.Disbursement.ExceedsApproved");
    }

    private static CreditLineVersionInput OriginacionDeTramos() =>
        Origination.OriginacionDePrueba.VersionDeLinea() with { Amortization = new AmortizationInput { AllowsTranches = true } };
}
