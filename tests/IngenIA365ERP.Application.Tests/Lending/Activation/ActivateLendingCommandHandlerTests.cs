using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// Feature 013, US8, T0724 (FR-140–FR-143, T84, C60–C63; data-model §30.1): la activación con cartera previa sobre la carga de
/// <see cref="ActivacionConCarteraDePrueba"/>, aplicada con su cuadre y su conciliación y con las diferencias aceptadas con acta.
/// <list type="bullet">
/// <item>Negativas: fecha distinta del día siguiente al corte, registro después del plazo, diferencias sin aceptar, un requisito pendiente, el acta que
/// no es de esa carga, una segunda activación y cobros provisionales que ya no validan.</item>
/// <item>El recálculo: con la misma huella sigue; con otra, nace <c>CheckVersion + 1</c> con sus filas (las marcas de ventas se copian), queda guardada
/// aunque la activación responda <c>DifferencesNotAccepted</c>.</item>
/// <item>Lo que nace: créditos con origen <c>Migration</c> y su número, condiciones selladas, el plan conservando el capital por cuota, el control al
/// corte, las partes, garantías, pagaré, vínculos de forma de pago y la preferencia, el saldo inicial por concepto <b>sin mensajes contables</b>, el
/// castigado, el cierre base con su historial, lo del deudor (lo registrado a mano prevalece), las ventas enlazadas y la obligación en cero, los no
/// aplicados «Cartera sin activar», los cobros provisionales guardados sin aplicar, <c>LND_Setup</c>, la carga <c>Activated</c>, el destino habilitado,
/// <c>CarteraActivada</c> y el numerador por encima de lo migrado.</item>
/// <item>La liquidación de cada crédito al corte es lo cargado; la misma clave y el mismo cuerpo devuelven la misma respuesta.</item>
/// </list>
/// InMemory con el reloj fijo y Contabilidad doble. (nuevo)
/// </summary>
public sealed class ActivateLendingCommandHandlerTests : IAsyncLifetime
{
    private static readonly DateOnly Corte = ActivacionConCarteraDePrueba.Corte;
    private ActivacionConCarteraDePrueba _a = null!;

    public async Task InitializeAsync()
    {
        _a = await ActivacionConCarteraDePrueba.CrearAsync();
        await _a.AplicarAsync();
    }

    public Task DisposeAsync()
    {
        _a.Dispose();
        return Task.CompletedTask;
    }

    private IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => _a.Db;

    private async Task<LendingActivationDto> ActivadaAsync(Guid? cobros = null)
    {
        await _a.AceptarTodoAsync();
        var r = await _a.ActivarAsync(_a.Orden(_a.Acta(), cobros: cobros));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private Task<Domain.Entities.Lending.Loans.Loan> CreditoAsync(string numero) => Db.Loans.AsNoTracking().SingleAsync(l => l.Number == numero);

    // ---------------------------------------------------------------------------------------------------------- negativas --

    [Fact]
    public async Task Una_fecha_que_no_es_el_dia_siguiente_al_corte_responde_CutoffInvalid()
    {
        await _a.AceptarTodoAsync();

        var r = await _a.ActivarAsync(_a.Orden(_a.Acta(), Corte.AddDays(2)));

        r.Error.Code.Should().Be(ErroresDeActivacion.CodigoCorteInvalido);
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { expected = ActivacionConCarteraDePrueba.Activacion });
    }

    [Fact]
    public async Task Registrada_despues_del_plazo_responde_DeadlinePassed()
    {
        await _a.AceptarTodoAsync();

        var r = await _a.ActivarAsync(_a.Orden(_a.Acta()), registro: Corte.AddDays(6));

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoPlazoVencido);
        JsonSerializer.SerializeToElement(((ErrorConDatos)r.Error).Data).GetProperty("deadline").GetString().Should().Be(Corte.AddDays(5).ToString("yyyy-MM-dd"));
        (await Db.Loans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_aceptar_las_diferencias_responde_DifferencesNotAccepted_y_no_nace_nada()
    {
        var r = await _a.ActivarAsync(_a.Orden(_a.Acta()));

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoDiferenciasSinAceptar);
        JsonSerializer.SerializeToElement(((ErrorConDatos)r.Error).Data).GetProperty("pending").GetInt32().Should().BeGreaterThan(0);
        (await Db.Loans.CountAsync()).Should().Be(0);
        (await Db.LendingActivations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_un_requisito_pendiente_responde_RequirementsMissing_con_la_lista()
    {
        await _a.AceptarTodoAsync();
        _a.Ambiente.EsProduccion.Returns(true);

        var r = await _a.ActivarAsync(_a.Orden(_a.Acta()));

        r.Error.Code.Should().Be(ErroresDeActivacion.CodigoFaltanRequisitos);
        JsonSerializer.SerializeToElement(((ErrorConDatos)r.Error).Data).GetProperty("missing").EnumerateArray().Select(m => m.GetProperty("code").GetString())
            .Should().BeEquivalentTo([RequisitosDeActivacion.RecaudadorRegistrado, RequisitosDeActivacion.TesoreriaRegistrada]);
        (await Db.Loans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_acta_o_con_un_punto_que_no_es_de_esa_carga_responde_ItemMismatch()
    {
        await _a.AceptarTodoAsync();

        (await _a.ActivarAsync(_a.Orden(Guid.Empty))).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await _a.ActivarAsync(_a.Orden(_a.Acta(Guid.NewGuid())))).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await _a.ActivarAsync(_a.Orden(_a.Acta(tipoDeSujeto: LendingDecisionSubjects.RiskPolicyVersion)))).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await Db.LendingActivations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_segunda_activacion_responde_AlreadyActivated()
    {
        await ActivadaAsync();

        var r = await _a.ActivarAsync(_a.Orden(_a.Acta()));

        r.Error.Code.Should().Be(ErroresDeActivacion.CodigoYaActivada);
        (await Db.LendingActivations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Cobros_provisionales_que_ya_no_validan_impiden_activar_y_no_queda_nada()
    {
        await _a.AceptarTodoAsync();
        var adjunto = await _a.AdjuntoDeCobrosAsync(("FV-MIG", "100000", Corte.AddDays(2).ToString("yyyy-MM-dd"), "RC-1"));

        var invalido = await _a.ActivarAsync(_a.Orden(_a.Acta(), cobros: adjunto));
        var excede = await _a.ActivarAsync(_a.Orden(_a.Acta(), cobros: await _a.AdjuntoDeCobrosAsync(
            ("FV-POS", "700000", Corte.AddDays(2).ToString("yyyy-MM-dd"), "RC-2"))));

        invalido.Error.Code.Should().Be(ImportErrors.InvalidCode, "una venta de la migración no lleva cobros provisionales");
        excede.Error.Code.Should().Be(ErroresDeMigracion.CodigoCobrosExcedenLaVenta);
        (await Db.LendingActivations.CountAsync()).Should().Be(0);
    }

    // -------------------------------------------------------------------------------------------------------- recálculo --

    [Fact]
    public async Task Con_otra_huella_nace_la_version_siguiente_guardada_y_hay_que_volver_a_aceptar()
    {
        await _a.AceptarTodoAsync();
        var carga = await Db.MigrationLoads.AsNoTracking().SingleAsync();
        carga.CurrentCheckVersion.Should().Be(1);
        _a.SaldoContable = c => c.Role == "Capital" ? 1m : null;

        var r = await _a.ActivarAsync(_a.Orden(_a.Acta()));

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoDiferenciasSinAceptar);
        var recalculada = await Db.MigrationLoads.AsNoTracking().SingleAsync();
        recalculada.CurrentCheckVersion.Should().Be(2);
        recalculada.BalanceCheckSha256.Should().NotBe(carga.BalanceCheckSha256);
        recalculada.IsBalanced.Should().BeFalse();
        (await Db.MigrationLoadDifferences.AsNoTracking().CountAsync(d => d.CheckVersion == 2 && d.Kind == MigrationDifferenceKind.BalanceCheck))
            .Should().BeGreaterThan(0);
        var ventas = await Db.MigrationSaleReconciliations.AsNoTracking().Where(v => v.CheckVersion == 2).ToListAsync();
        ventas.Select(v => (v.InvoiceNumber, v.Outcome)).Should().BeEquivalentTo(
        [
            ("FV-MIG", SaleReconciliationOutcome.Migrated), ("FV-CAN", SaleReconciliationOutcome.CancelledBeforeCutoff),
            ("FV-SIN", SaleReconciliationOutcome.NotMigratedWithBalance),
        ]);
        (await Db.Loans.CountAsync()).Should().Be(0);

        // Vuelve a aceptar la versión 2 y activa.
        await _a.AceptarTodoAsync();
        (await _a.ActivarAsync(_a.Orden(_a.Acta()))).IsSuccess.Should().BeTrue();
        (await Db.LendingActivations.AsNoTracking().SingleAsync()).CheckVersion.Should().Be(2);
    }

    [Fact]
    public async Task Las_marcas_desde_la_lista_se_copian_a_la_version_recalculada()
    {
        var sin = await Db.MigrationSaleReconciliations.AsNoTracking().SingleAsync(r => r.InvoiceNumber == "FV-SIN");
        var marcada = await new MarkMigrationSaleCommandHandler(Db, _a.G.Actor, _a.G.Reloj).Handle(new MarkMigrationSaleCommand(_a.Carga.PublicId,
            sin.MessagePublicId, SaleReconciliationOutcome.OutsideLendingD29, "Venta de servicios a asociados") { OperationKey = Guid.NewGuid() }, default);
        marcada.IsSuccess.Should().BeTrue();
        Db.ChangeTracker.Clear();
        await _a.AceptarTodoAsync();
        _a.SaldoContable = c => c.Role == "Capital" ? 1m : null;

        (await _a.ActivarAsync(_a.Orden(_a.Acta()))).Error.Code.Should().Be(ErroresDeMigracion.CodigoDiferenciasSinAceptar);

        var copiada = await Db.MigrationSaleReconciliations.AsNoTracking().SingleAsync(r => r.InvoiceNumber == "FV-SIN" && r.CheckVersion == 2);
        (copiada.Outcome, copiada.MarkedFromList, copiada.MarkReason).Should().Be((SaleReconciliationOutcome.OutsideLendingD29, true, "Venta de servicios a asociados"));
    }

    // ------------------------------------------------------------------------------------------------------- lo que nace --

    [Fact]
    public async Task Nacen_los_creditos_con_su_numero_sus_condiciones_su_plan_y_su_control_al_corte()
    {
        var r = await ActivadaAsync();

        r.Loans.Should().Be(3);
        var cr100 = await CreditoAsync("CR-100");
        cr100.Should().BeEquivalentTo(new
        {
            Origin = LoanOrigin.Migration, Status = LoanStatus.Active, DebtorPersonId = _a.O.Ana.Id, CreditLineId = _a.LineaId, ApprovedAmount = _a.Capital,
            OpenedOn = Corte, SourceInvoiceNumber = "FV-MIG", SourcePaymentPublicId = (Guid?)_a.Ventas["FV-MIG"].Pago,
            SaleObligationStatus = (SaleObligationStatus?)SaleObligationStatus.Migrated, DebtorKind = DebtorKind.Associate,
        }, o => o.ExcludingMissingMembers());
        cr100.MigrationLoadId.Should().Be(_a.Carga.Id);
        (await CreditoAsync("CR-200")).Status.Should().Be(LoanStatus.Active);
        var castigado = await CreditoAsync("CO-300");
        (castigado.Status, castigado.WrittenOffOn).Should().Be((LoanStatus.WrittenOff, Corte.AddDays(-200)));

        (await Db.LoanControls.AsNoTracking().Where(c => c.Loan!.MigrationLoadId == _a.Carga.Id).Select(c => c.LastAccruedDate).ToListAsync())
            .Should().HaveCount(4, "los tres de la carga y la obligación en cero de la venta cancelada").And.OnlyContain(d => d == Corte);

        var condiciones = await Db.LoanTermsVersions.AsNoTracking().SingleAsync(t => t.LoanId == cr100.Id);
        condiciones.Should().BeEquivalentTo(new
        {
            VersionNumber = 1, Cause = TermsVersionCause.Migration, RulesVersion = VersionesDeReglas.Vigente, AnnualRate = (decimal?)0.22m,
            DefaultRateKind = DefaultRateKind.RelativeToCurrent, DefaultRateFactor = (decimal?)1.5m, Periodicity = RepaymentPeriodicity.Monthly, Installments = 6,
            RestructuringCount = 1, RestructuredOn = (DateOnly?)Corte.AddDays(-200), EffectiveValueDate = Corte,
        }, o => o.ExcludingMissingMembers());

        var plan = await Db.RepaymentScheduleVersions.AsNoTracking().SingleAsync(v => v.LoanId == cr100.Id);
        (plan.IsMigratedPlan, plan.VersionNumber, plan.InstallmentCount, plan.PrincipalBase).Should().Be((true, 1, 6, _a.Capital));
        (await Db.RepaymentInstallments.AsNoTracking().Where(i => i.ScheduleVersionId == plan.Id).OrderBy(i => i.InstallmentNumber).Select(i => i.Principal)
            .ToListAsync()).Should().OnlyContain(c => c == _a.Cuota, "el capital por cuota se conserva (D5)");
    }

    [Fact]
    public async Task El_saldo_inicial_va_por_concepto_al_corte_y_sin_mensajes_contables()
    {
        var r = await ActivadaAsync();

        var cr100 = await CreditoAsync("CR-100");
        var operacion = await Db.LoanOperations.AsNoTracking().SingleAsync(o => o.LoanId == cr100.Id);
        (operacion.Kind, operacion.ValueDate, operacion.AccountingDate, operacion.MigrationLoadId, operacion.Amount)
            .Should().Be((LoanOperationKind.OpeningBalance, Corte, Corte, (int?)_a.Carga.Id, _a.Capital));
        var filas = await Db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == cr100.Id).ToListAsync();
        filas.Select(f => (f.Concept, f.InstallmentNumber, f.Amount)).Should().BeEquivalentTo(
        [
            (LoanConcept.Principal, (int?)null, _a.Capital),
            (LoanConcept.CurrentInterest, (int?)1, 30_000m),
            (LoanConcept.CurrentInterest, (int?)2, 25_000m),
            (LoanConcept.CurrentInterestInSuspense, (int?)3, 20_000m),
            (LoanConcept.DefaultInterest, (int?)1, 2_000m),
        ]);
        filas.Should().OnlyContain(f => f.Kind == LoanTransactionKind.OpeningBalance && f.ValueDate == Corte && f.MessagePublicId == null && f.OperationId == operacion.Id);
        r.OpeningTransactions.Should().Be(await Db.LoanTransactions.CountAsync());

        var castigado = await CreditoAsync("CO-300");
        (await Db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == castigado.Id).Select(t => new { t.Concept, t.Amount }).ToListAsync())
            .Should().BeEquivalentTo([new { Concept = LoanConcept.Principal, Amount = 800_000m }, new { Concept = LoanConcept.CurrentInterest, Amount = 50_000m }]);

        var deCartera = await Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginModule == "CAR").Select(m => m.Type).ToListAsync();
        deCartera.Should().BeEquivalentTo([CargaDeCarteraAplicadaV1.Type, CarteraActivadaV1.Type], "el saldo inicial no emite hechos (FR-140)");
        (await Db.AccountingFactParts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Nacen_las_partes_las_garantias_el_pagare_los_vinculos_de_forma_de_pago_y_la_preferencia()
    {
        await ActivadaAsync();

        var cr100 = await CreditoAsync("CR-100");
        var cr200 = await CreditoAsync("CR-200");
        (await Db.LoanParties.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(new { LoanId = cr100.Id, PersonId = _a.O.Beto.Id, Role = CreditPartyRole.CoDebtor },
            o => o.ExcludingMissingMembers());
        (await Db.Collaterals.AsNoTracking().SingleAsync(c => c.LoanId == cr100.Id)).Should().BeEquivalentTo(new
        {
            Kind = CollateralKind.Pledge, Value = 5_000_000m, Status = CollateralStatus.Valid, RegistrationReference = "ABC123", OwnerPersonId = _a.O.Ana.Id,
        }, o => o.ExcludingMissingMembers());
        (await Db.PromissoryNotes.AsNoTracking().SingleAsync(p => p.LoanId == cr100.Id)).Should().BeEquivalentTo(new
        {
            Kind = PromissoryNoteKind.Physical, Number = "PG-100", Status = PromissoryNoteStatus.Signed,
        }, o => o.ExcludingMissingMembers());
        var vinculos = await Db.LoanPayrollLinks.AsNoTracking().ToListAsync();
        vinculos.Should().HaveCount(2).And.OnlyContain(v => v.Origin == LoanPayrollLinkOrigin.Migration && v.OperationId != null);
        vinculos.Single(v => v.LoanId == cr200.Id).Should().BeEquivalentTo(new
        {
            RepaymentChannel = RepaymentChannel.OwnPayroll, IsOwnPayroll = true, PaymasterPersonId = (int?)_a.O.Beto.Id,
        }, o => o.ExcludingMissingMembers());
        vinculos.Single(v => v.LoanId == cr100.Id).PaymasterPersonId.Should().BeNull();
        (await Db.PrepaymentPreferences.AsNoTracking().SingleAsync(p => p.LoanId == cr100.Id)).Should().BeEquivalentTo(new
        {
            Choice = (PrepaymentChoice?)PrepaymentChoice.ReduceTerm, Origin = PreferenceOrigin.Migration, VersionNumber = 1,
        }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Nace_el_cierre_base_al_corte_con_sus_lineas_el_general_y_el_historial_migrado()
    {
        var r = await ActivadaAsync();

        var activacion = await Db.LendingActivations.AsNoTracking().SingleAsync();
        var cierre = await Db.MonthlyCloses.AsNoTracking().SingleAsync();
        cierre.Should().BeEquivalentTo(new { Kind = MonthlyCloseKind.Base, CloseDate = Corte, ActivationId = (int?)activacion.Id, LoanCount = 2 },
            o => o.ExcludingMissingMembers());
        r.BaseClosePublicId.Should().Be(cierre.PublicId);
        (await Db.MonthlyCloseLoans.AsNoTracking().Where(l => l.MonthlyCloseId == cierre.Id).Select(l => l.TotalImpairment).SumAsync()).Should().Be(30_000m);
        (await Db.MonthlyCloseGeneralImpairments.AsNoTracking().Where(g => g.MonthlyCloseId == cierre.Id).SumAsync(g => g.TotalAmount)).Should().Be(22_000m);
        (await Db.LoanArrearsHistory.AsNoTracking().ToListAsync()).Should().HaveCount(2).And.OnlyContain(h => h.Origin == ArrearsHistoryOrigin.Migration);
    }

    [Fact]
    public async Task Nace_lo_del_deudor_y_lo_registrado_a_mano_prevalece()
    {
        Db.CreditLimits.Add(CreditLimit.NuevaVersion(_a.O.Dario.Id, CreditLimitKind.Consolidated, null, 3_000_000m, CreditLimitOrigin.Assigned,
            Corte.AddDays(-30), 1, _a.G.UtcNow.AddDays(-30), "Cupo del comité", null));
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        await ActivadaAsync();

        var cupos = await Db.CreditLimits.AsNoTracking().Where(c => c.Origin == CreditLimitOrigin.Migration).ToListAsync();
        cupos.Should().ContainSingle().Which.Should().BeEquivalentTo(new { PersonId = _a.O.Carla.Id, Amount = 2_000_000m, VersionNumber = 1 },
            o => o.ExcludingMissingMembers());
        var bloqueos = await Db.DebtorBlocks.AsNoTracking().Where(b => b.Reason == DebtorBlockReason.Garnishment || b.Reason == DebtorBlockReason.Insolvency)
            .ToListAsync();
        bloqueos.Select(b => (b.PersonId, b.Reason)).Should().BeEquivalentTo([(_a.O.Beto.Id, DebtorBlockReason.Garnishment), (_a.O.Carla.Id, DebtorBlockReason.Insolvency)]);
        bloqueos.Single(b => b.Reason == DebtorBlockReason.Garnishment).LoanId.Should().Be((await CreditoAsync("CR-200")).Id);
        (await Db.InsolvencyProceedings.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(new
        {
            PersonId = _a.O.Carla.Id, Status = InsolvencyProceedingStatus.Negotiating, StartDate = Corte.AddDays(-15), MigrationLoadId = (int?)_a.Carga.Id,
        }, o => o.ExcludingMissingMembers());
        _a.O.Agregadas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PersonPublicId = _a.O.Ana.PublicId, Purpose = DataAuthorizationPurpose.CreditBureauReport, Origin = DataAuthorizationOrigin.Migration,
        }, o => o.ExcludingMissingMembers());
        var aviso = await Db.PriorNotices.AsNoTracking().SingleAsync();
        aviso.Should().BeEquivalentTo(new
        {
            EpisodeStartDate = Corte.AddDays(-60), MigratedFirstSentOn = (DateOnly?)Corte.AddDays(-50), MigratedReportedNegativeSince = (DateOnly?)new DateOnly(2026, 11, 1),
            MigratedBureauCode = "DATACREDITO", MigratedEvidenceReference = "G-1", PersonId = _a.O.Ana.Id,
        }, o => o.ExcludingMissingMembers());
        (await Db.CreditReportClaims.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(new
        {
            Status = CreditReportClaimStatus.InProcess, BureauCode = "DATACREDITO", PersonId = _a.O.Ana.Id, ReceivedOn = Corte.AddDays(-5),
        }, o => o.ExcludingMissingMembers());
        (await Db.RelatedParties.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(new
        {
            PersonId = _a.O.Beto.Id, Basis = RelatedPartyBasis.BoardMember, Source = RelatedPartySource.Migration,
        }, o => o.ExcludingMissingMembers());

        var conteos = JsonSerializer.SerializeToElement(JsonDocument.Parse((await Db.LendingActivations.AsNoTracking().SingleAsync()).CountsJson).RootElement);
        conteos.GetProperty("manualPrevails").GetProperty("CreditLimit").GetInt32().Should().Be(1, "el cupo a mano de Darío prevalece");
        conteos.GetProperty("creditLimits").GetInt32().Should().Be(1);
        conteos.GetProperty("authorizations").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Las_ventas_quedan_enlazadas_y_la_cancelada_nace_como_obligacion_en_cero_por_encima_de_lo_migrado()
    {
        await ActivadaAsync();

        var filas = await Db.MigrationSaleReconciliations.AsNoTracking().ToListAsync();
        filas.Single(f => f.InvoiceNumber == "FV-MIG").ResultingLoanId.Should().Be((await CreditoAsync("CR-100")).Id);
        filas.Single(f => f.InvoiceNumber == "FV-SIN").ResultingLoanId.Should().BeNull();
        var idEnCero = filas.Single(f => f.InvoiceNumber == "FV-CAN").ResultingLoanId;
        var enCero = await Db.Loans.AsNoTracking().SingleAsync(l => l.Id == idEnCero);
        enCero.Should().BeEquivalentTo(new
        {
            Number = "CR-000201", Status = LoanStatus.PaidOff, PaidOffOn = (DateOnly?)Corte, SaleObligationStatus = (SaleObligationStatus?)SaleObligationStatus.CancelledBeforeCutoff,
            SourceInvoiceNumber = "FV-CAN", SourcePaymentPublicId = (Guid?)_a.Ventas["FV-CAN"].Pago, CreditLineId = _a.LineaId, DebtorPersonId = _a.O.Ana.Id,
        }, o => o.ExcludingMissingMembers());
        (await Db.LoanControls.AsNoTracking().CountAsync(c => c.LoanId == enCero.Id)).Should().Be(1);
        (await Db.LoanOperations.AsNoTracking().CountAsync(o => o.LoanId == enCero.Id)).Should().Be(0, "la obligación en cero no tiene movimientos");
    }

    [Fact]
    public async Task Las_cuotas_de_nomina_propia_vencidas_antes_del_registro_quedan_no_aplicadas_con_su_alerta()
    {
        var r = await ActivadaAsync();

        r.NonDeductionsMarked.Should().Be(1);
        var cr200 = await CreditoAsync("CR-200");
        var noAplicada = await Db.InstallmentNonDeductions.AsNoTracking().SingleAsync();
        noAplicada.Should().BeEquivalentTo(new
        {
            LoanId = cr200.Id, InstallmentNumber = 1, DueDate = Corte.AddDays(2), Origin = NonDeductionOrigin.Activation, Reason = NonDeductionReason.Other,
            ReasonText = ActivateLendingCommandHandler.MotivoCarteraSinActivar, ActivationId = (int?)(await Db.LendingActivations.AsNoTracking().SingleAsync()).Id,
        }, o => o.ExcludingMissingMembers());
        noAplicada.ExpectedAmount.Should().BeGreaterThan(300_000m, "la cuota esperada lleva su interés");
        _a.O.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.CuotaNoDescontadaPorNomina && a.EntityPublicId == cr200.PublicId);
    }

    [Fact]
    public async Task Los_cobros_provisionales_se_guardan_sin_aplicar_con_la_huella_del_archivo()
    {
        await _a.AceptarTodoAsync();
        var adjunto = await _a.AdjuntoDeCobrosAsync(("FV-POS", "250000", Corte.AddDays(2).ToString("yyyy-MM-dd"), "RC-10"),
            ("FV-POS", "100000", Corte.AddDays(3).ToString("yyyy-MM-dd"), "RC-11"));

        var r = await _a.ActivarAsync(_a.Orden(_a.Acta(), cobros: adjunto));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.ProvisionalCollectionsStored.Should().Be(2);
        var activacion = await Db.LendingActivations.AsNoTracking().SingleAsync();
        (activacion.ProvisionalCollectionsCount, activacion.ProvisionalCollectionsReviewAttachmentPublicId).Should().Be((2, (Guid?)adjunto));
        activacion.ProvisionalCollectionsFileSha256.Should().HaveLength(64);
        var cobros = await Db.ProvisionalCollections.AsNoTracking().OrderBy(c => c.RowNumber).ToListAsync();
        cobros.Select(c => (c.InvoiceNumber, c.Amount, c.AccountingReference)).Should().Equal(("FV-POS", 250_000m, "RC-10"), ("FV-POS", 100_000m, "RC-11"));
        cobros.Should().OnlyContain(c => c.ActivationId == activacion.Id && c.LoanId == null && c.OperationId == null && c.AppliedAt == null && c.MessagePublicId == null);
    }

    [Fact]
    public async Task Queda_activada_la_carga_el_setup_el_destino_y_CarteraActivada_con_el_capital()
    {
        var r = await ActivadaAsync();

        var activacion = await Db.LendingActivations.AsNoTracking().SingleAsync();
        activacion.Should().BeEquivalentTo(new
        {
            ActivationDate = ActivacionConCarteraDePrueba.Activacion, CutoffDate = Corte, CheckVersion = 1, IsProductionEnvironment = false,
            PayrollWarningAcknowledged = true, NonDeductionsMarked = 1,
        }, o => o.ExcludingMissingMembers());
        activacion.CountsJson.Should().Contain("\"loans\":2").And.Contain("\"writtenOff\":1");
        activacion.RequirementsJson.Should().Contain(RequisitosDeActivacion.DiferenciasAceptadas);
        var setup = await Db.LendingSetups.AsNoTracking().SingleAsync();
        (setup.ActivationDate, setup.ActivationId).Should().Be((ActivacionConCarteraDePrueba.Activacion, (int?)activacion.Id));
        (await Db.MigrationLoads.AsNoTracking().SingleAsync()).Status.Should().Be(MigrationLoadStatus.Activated);
        (await Db.IntegrationParticipants.AsNoTracking().Where(p => p.Code == "CAR").OrderBy(p => p.ValidFrom).LastAsync()).IsEnabled.Should().BeTrue();
        var mensaje = await Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CarteraActivadaV1.Type);
        mensaje.PublicId.Should().Be(activacion.ActivatedMessagePublicId);
        var contenido = JsonDocument.Parse(mensaje.PayloadJson).RootElement;
        contenido.GetProperty("principalTotal").GetDecimal().Should().Be(_a.Capital + 1_200_000m);
        r.Messages.Should().ContainSingle(m => m.Type == CarteraActivadaV1.Type);
    }

    [Fact]
    public async Task La_liquidacion_de_cada_credito_al_corte_es_lo_cargado()
    {
        await ActivadaAsync();

        var cr100 = await CreditoAsync("CR-100");
        var cargado = await new CargadorDeReconstruccion(Db, _a.D.Series()).CargarAsync(cr100.Id, null, Corte, default);
        cargado!.Listo.Should().BeTrue(cargado.Error?.Message);
        var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, Corte);

        estado.Saldos.GroupBy(s => s.Concepto).ToDictionary(g => g.Key, g => g.Sum(s => s.Valor)).Should().BeEquivalentTo(new Dictionary<LoanConcept, decimal>
        {
            [LoanConcept.Principal] = _a.Capital, [LoanConcept.CurrentInterest] = 55_000m, [LoanConcept.CurrentInterestInSuspense] = 20_000m,
            [LoanConcept.DefaultInterest] = 2_000m,
        });
        estado.Cuotas.Select(c => c.CapitalPendiente).Should().OnlyContain(c => c == _a.Cuota);
    }

    [Fact]
    public async Task La_misma_clave_con_el_mismo_cuerpo_devuelve_la_misma_respuesta_sin_repetir_nada()
    {
        await _a.AceptarTodoAsync();
        var orden = _a.Orden(_a.Acta());
        _a.G.HoyLocal = ActivacionConCarteraDePrueba.Registro;

        var primera = await ConClaveAsync(orden);
        var repetida = await ConClaveAsync(orden);

        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? $"{primera.Error.Code}: {primera.Error.Message}" : string.Empty);
        repetida.IsSuccess.Should().BeTrue(repetida.IsFailure ? $"{repetida.Error.Code}: {repetida.Error.Message}" : string.Empty);
        repetida.Value.ActivationPublicId.Should().Be(primera.Value.ActivationPublicId);
        (await Db.LendingActivations.CountAsync()).Should().Be(1);
        (await Db.Loans.CountAsync(l => l.Origin == LoanOrigin.Migration)).Should().Be(4, "tres de la carga y la obligación en cero, una sola vez");
    }

    [Fact]
    public async Task Un_cupo_otorgado_a_mano_que_excede_un_limite_con_lo_que_nace_queda_por_revalidar()
    {
        _a.Dispose();
        _a = await ActivacionConCarteraDePrueba.CrearAsync(cuotaDeAna: 25_000_000m);
        await _a.AplicarAsync();
        Db.CreditLimits.Add(CreditLimit.NuevaVersion(_a.O.Ana.Id, CreditLimitKind.Consolidated, null, 5_000_000m, CreditLimitOrigin.Assigned,
            Corte.AddDays(-20), 1, _a.G.UtcNow.AddDays(-20), "Cupo del comité", null));
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var r = await ActivadaAsync();

        r.RevalidatedLimits.Should().Be(1);
        (await Db.DebtorBlocks.AsNoTracking().SingleAsync(b => b.Reason == DebtorBlockReason.LimitRevalidation)).PersonId.Should().Be(_a.O.Ana.Id);
        _a.O.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.CupoPorRevalidar);
        (await Db.LendingActivations.AsNoTracking().SingleAsync()).CreditLimitsToRevalidate.Should().Be(1);
    }

    private async Task<Result<LendingActivationDto>> ConClaveAsync(ActivateLendingCommand orden)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IApplicationDbContext>(Db);
        servicios.AddSingleton(_a.G.Actor);
        servicios.AddSingleton(Substitute.For<IAuditService>());
        servicios.AddSingleton(new EstadoDeLaOperacion());
        var behavior = new IdempotencyBehavior<ActivateLendingCommand, Result<LendingActivationDto>>(servicios.BuildServiceProvider(),
            NullLogger<IdempotencyBehavior<ActivateLendingCommand, Result<LendingActivationDto>>>.Instance);
        var r = await behavior.Handle(orden, _ => _a.Activador().Handle(orden, default), default);
        Db.ChangeTracker.Clear();
        return r;
    }
}
