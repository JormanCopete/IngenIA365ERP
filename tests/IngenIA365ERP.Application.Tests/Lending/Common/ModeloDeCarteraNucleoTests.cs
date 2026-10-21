using FluentAssertions;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 013, T0137 (data-model §0.1, §0.2, §0.13, §14; DT §7 C42, C43, C46, C112): el modelo real de las tablas de
/// <c>CarteraNucleo</c> en los dos motores. Toda entidad de <c>Domain.Entities.Lending</c> mapea a una tabla de la lista, con su
/// <c>UK_{Tabla}_PublicId</c>, filtro de borrado lógico y FK <c>Restrict</c>; ninguna FK sale hacia Contabilidad, Comercial, Nómina
/// ni Ahorros; los hechos de alto volumen son <c>bigint</c>; ningún nombre de índice pasa de 63 caracteres; los únicos de hechos y
/// controles no filtran por borrado; las marcas H y <c>[SinDiffDeAuditoria]</c> son exactamente las fijadas.
///
/// <para>US9 (bloque 12B, T0697): suma las siete tablas de <c>CarteraRiesgo</c> (data-model §23.1–§23.6, §23.10) con sus marcas, y las FK de
/// <c>LND_AccountingFactParts</c> hacia el cierre y su reapertura, que la base dejó sin crear (C66).</para>
///
/// <para>K2 (fase 15, T0827): suma las ocho tablas de <c>CarteraLibranza</c> con sus marcas: la línea del archivo, el resultado y el seguimiento
/// son hechos; la línea va sin diferencias de auditoría (C46).</para>
///
/// <para>K3 (fases 16 y 17, T0875): suma las nueve tablas de <c>CarteraNovedades</c> (data-model §25.1–§25.6, §23.7); ninguna es hecho: las
/// novedades, condonaciones, lotes y evaluaciones avanzan de estado.</para>
///
/// <para>K4 (fase 18, T0939): suma las once tablas de <c>CarteraCobranza</c> (data-model §26.3–§26.10). Son hechos la historia de etapas, la
/// asignación, la gestión, la promesa, la cuota del acuerdo y la actuación jurídica; la etapa, la regla, el acuerdo, el proceso y la campaña
/// cambian de vigencia o de estado.</para>
///
/// <para>K5 (fase 21, T0997, T1001): suma las dos tablas de <c>CarteraInformes</c> (data-model §28): el archivo regulatorio avanza de estado; su
/// línea de central es hecho <c>bigint</c> sin diferencias de auditoría (C46).</para>
///
/// <para>K6a (fase 22, T1032, T1033): suma las dos tablas de <c>CarteraPerdidaEsperada</c> (data-model §23.8, §23.9): la elección voluntaria y la carga
/// complementaria del historial son hechos; la carga, sin diferencias de auditoría (C46). La FK de <c>LND_LoanArrearsHistory.ArrearsHistoryLoadId</c>
/// nace con ella.</para>
/// </summary>
public class ModeloDeCarteraNucleoTests
{
    /// <summary>Las 74 + 17 de data-model §14 más las dos del grupo conectado (C112), y las de CarteraRiesgo, CarteraLibranza y CarteraNovedades.</summary>
    private static readonly string[] Tablas =
    [
        // Configuración (19)
        "LND_Setup", "LND_CreditLines", "LND_CreditLineVersions", "LND_CommercialTermEquivalences", "LND_CreditLineRateBands",
        "LND_CreditLineDocumentRequirements", "LND_CreditLineInsurances", "LND_CreditLineCollateralRules", "LND_FactTypes",
        "LND_NumberSequences", "LND_UserCreditLineScopes", "LND_AttributionMatrices", "LND_AttributionLevels", "LND_AttributionRules",
        "LND_GoverningBodies", "LND_GoverningBodyRuleVersions", "LND_GoverningBodyMembers", "LND_RelatedParties",
        "LND_ContributionBalanceLoads",
        // Originación (20)
        "LND_CreditApplications", "LND_CreditApplicationParties", "LND_CreditApplicationDocuments", "LND_CreditApplicationChecks",
        "LND_CreditScores", "LND_CreditDecisions", "LND_Sessions", "LND_SessionAttendances", "LND_SessionItems", "LND_SessionVotes",
        "LND_CaseParticipations", "LND_ScreeningResults", "LND_BureauQueries", "LND_Collaterals", "LND_PromissoryNotes",
        "LND_CreditLimits", "LND_DebtorBlocks", "LND_LoanNumberReservations", "LND_DebtorControls", "LND_PrepaymentPreferences",
        // Crédito y libro (17)
        "LND_Loans", "LND_LoanParties", "LND_LoanTermsVersions", "LND_LoanOperations", "LND_LoanTransactions",
        "LND_RepaymentScheduleVersions", "LND_RepaymentInstallments", "LND_LoanControls", "LND_LoanDailyPositions",
        "LND_InstallmentNonDeductions", "LND_InsuranceClaims", "LND_InsolvencyProceedings", "LND_DisbursementOrders",
        "LND_DisbursementOrderResults", "LND_DisbursementFiles", "LND_DisbursementFileLines", "LND_LoanPayrollLinks",
        // Integración y mensajes (7)
        "LND_InboundMessages", "LND_InboundMessageEvaluations", "LND_AccountingFactParts", "LND_ProcessRuns", "LND_ProcessRunErrors",
        "LND_DebtorMessages", "LND_DebtorMessageEvents",
        // Datos de K4 (2) y documentos (3, I5)
        "LND_PriorNotices", "LND_CreditReportClaims", "LND_IssuedDocuments", "LND_IssuedDocumentLoans", "LND_IssuedDocumentEvents",
        // Migración y activación (6 + 17)
        "LND_MigrationLoads", "LND_MigrationLoadParts", "LND_MigrationLoadDifferences", "LND_MigrationSaleReconciliations",
        "LND_ProvisionalCollections", "LND_Activations",
        "LND_MigrationLoans", "LND_MigrationInstallments", "LND_MigrationBalances", "LND_MigrationRiskData", "LND_MigrationRestructurings",
        "LND_MigrationCollaterals", "LND_MigrationCoDebtors", "LND_MigrationCreditLimits", "LND_MigrationBlocks",
        "LND_MigrationAuthorizations", "LND_MigrationPriorNotices", "LND_MigrationBureauReports", "LND_MigrationClaims",
        "LND_MigrationRelatedParties", "LND_MigrationChargedOff", "LND_MigrationSales", "LND_MigrationArrearsHistory",
        // Grupo conectado (C112)
        "LND_ConnectedGroups", "LND_ConnectedGroupMembers",
        // Riesgo y cierres (US9, CarteraRiesgo)
        "LND_RiskPolicyVersions", "LND_MonthlyCloses", "LND_MonthlyCloseLoans", "LND_MonthlyCloseGeneralImpairments", "LND_MonthlyCloseReopenings",
        "LND_LoanArrearsHistory", "LND_AnalysisRatings",
        // Libranza (K2, CarteraLibranza; data-model §24.1–§24.4, §24.6–§24.8, §24.10)
        "LND_PayrollAgreements", "LND_PayrollAgreementVersions", "LND_PayrollAgreementContacts", "LND_PayrollAgreementResultCodes",
        "LND_PayrollDeductionFiles", "LND_PayrollDeductionFileLines", "LND_PayrollDeductionResults", "LND_NonDeductionFollowUps",
        // Novedades, condonaciones, castigo y evaluación (K3, CarteraNovedades; data-model §25.1–§25.6, §23.7)
        "LND_LoanNovelties", "LND_LoanNoveltyLoans", "LND_LoanNoveltyConsents", "LND_Waivers", "LND_WaiverLines", "LND_WriteOffBatches",
        "LND_WriteOffBatchItems", "LND_PortfolioEvaluations", "LND_PortfolioEvaluationLoans",
        // Cobranza (K4, CarteraCobranza; data-model §26.3–§26.10)
        "LND_MessageCampaigns", "LND_CollectionStages", "LND_CollectionStageHistory", "LND_CollectorAssignmentRules", "LND_CollectorAssignments",
        "LND_CollectionActions", "LND_PaymentPromises", "LND_PaymentAgreements", "LND_PaymentAgreementInstallments", "LND_LegalProcesses",
        "LND_LegalProcessActions",
        // Archivos regulatorios (K5, CarteraInformes; data-model §28)
        "LND_RegulatoryFiles", "LND_RegulatoryFileLines",
        // Pérdida esperada (K6a, CarteraPerdidaEsperada; data-model §23.8, §23.9)
        "LND_ImpairmentModelElections", "LND_ArrearsHistoryLoads",
        // Captaciones y aportes (feature 014, K7, CaptacionesAportes; data-model §3.1, §3.2, §3.9, §4.1–§4.4)
        "LND_MemberAccounts", "LND_MemberAccountParties", "LND_AccountPayoutOrders", "LND_MemberAccountOperations", "LND_MemberAccountTransactions",
        "LND_MemberAccountDailyPositions", "LND_MemberAccountControls",
        // Feature 014, US1 (T060–T063, CaptacionesAportes; data-model §2.1, §2.2, §2.5): las versiones de las líneas de captación y de aportes
        "LND_DepositLineVersions", "LND_DepositLineRateTiers", "LND_ContributionLineVersions",
        // Feature 014, US2 (T072, CaptacionesAportes; data-model §3.8): la cuenta exenta del GMF
        "LND_GmfExemptAccounts",
        // Feature 014, US3 (T081, CaptacionesAportes; data-model §3.5, §3.7): las cuotas exigibles y lo no descontado
        "LND_AccountInstallments", "LND_AccountInstallmentNonDeductions",
        // Feature 014, US3 (T086, research E58): las solicitudes de devolución y de cesión de aportes
        "LND_ContributionRequests",
        // Feature 014, US3 (T087; data-model §1.5.1): las cuentas que cubre un documento emitido
        "LND_IssuedDocumentAccounts",
        // Feature 014, US3 (T090; data-model §1.4.1, §1.4.2): los grupos de archivo y las equivalencias de concepto del convenio de pagaduría
        "LND_PayrollAgreementFileGroups", "LND_PayrollAgreementConceptEquivalences",
        // Feature 014, US10 (T096; data-model §6.1, §6.2): el retiro del asociado y las líneas de su liquidación
        "LND_MembershipWithdrawals", "LND_MembershipWithdrawalLines",
        // Feature 014, US12 (T202; data-model §6.3, §6.4): la decisión de asamblea y sus líneas
        "LND_AssemblyDistributions", "LND_AssemblyDistributionLines",
        // Feature 014, respuesta 90 C (E261): la base de los retornos que la cooperativa sube por plantilla
        "LND_AssemblyReturnBases",
        // Feature 014, US4 (T110, T114, T118; data-model §2.3, §3.6, §4.7–§4.9): el GMF del mes, los cargos y el débito automático
        "LND_GmfMonthlyUsage", "LND_DepositLineCharges", "LND_AccountCharges", "LND_AccountChargeExemptions", "LND_DirectDebitMandates",
        // Feature 014, T091 (decisión del dueño del 2026-10-18, D36; data-model §1.4.3): las cuotas de cuentas del archivo de un grupo, su resultado y lo
        // que la respuesta no pudo aplicar.
        "LND_PayrollDeductionFileAccountLines", "LND_PayrollDeductionAccountResults", "LND_PayrollDeductionUnmatchedLines",
        // Feature 014, US5 (T130; data-model §4.5, §4.6): la liquidación de intereses y la retención acumulada por titular y año
        "LND_InterestSettlements", "LND_WithholdingAccumulations",
        // Feature 014, US9 (T151; data-model §7.1): las filas de preparación de la migración de aportes y captaciones
        "LND_MigrationLineEquivalenceRows", "LND_MigrationContributionAccountRows", "LND_MigrationAccountRows", "LND_MigrationAccountPartyRows",
        "LND_MigrationAccountInstallmentRows", "LND_MigrationGmfExemptionRows", "LND_MigrationTaxYearRows",
        // Feature 014, US6 (T169; data-model §2.4, §3.3, §3.4, §7.1; research E134): el CDAT, su plan, la tasa negociada y su migración
        "LND_MemberAccountTermsVersions", "LND_TermDepositSchedules", "LND_NegotiatedRateRules", "LND_NegotiatedRateRequests",
        "LND_MigrationTermDepositRows",
        // Feature 014, US11 (T187; data-model §5.1, §5.2, §5.2.1, §7.1): bloqueos, embargos, respuestas al juzgado y su fila de migración
        "LND_AccountHolds", "LND_Garnishments", "LND_GarnishmentResponses", "LND_MigrationHoldRows",
        // Feature 014, US14 (T207; data-model §6.5, §6.6): el fallecimiento del titular y los saldos no reclamados
        "LND_DeceasedHolderCases", "LND_UnclaimedBalances",
        // Feature 014, US15 (T215; data-model §6.7, §6.8): la foto del fondo de liquidez y lo constituido
        "LND_LiquidityFundSnapshots", "LND_LiquidityFundHoldings",
    ];

    /// <summary>Las 17 de preparación (data-model §29.3) y las 7 de la migración de captaciones (014, data-model §7.1).</summary>
    private static readonly string[] Preparacion =
    [
        "MigrationLoanRow", "MigrationInstallmentRow", "MigrationBalanceRow", "MigrationRiskDataRow", "MigrationRestructuringRow",
        "MigrationCollateralRow", "MigrationCoDebtorRow", "MigrationCreditLimitRow", "MigrationBlockRow", "MigrationAuthorizationRow",
        "MigrationPriorNoticeRow", "MigrationBureauReportRow", "MigrationClaimRow", "MigrationRelatedPartyRow", "MigrationChargedOffRow",
        "MigrationSaleRow", "MigrationArrearsHistoryRow",
        // Feature 014, US9 (T151): las siete de la migración de aportes y captaciones.
        "MigrationLineEquivalenceRow", "MigrationContributionAccountRow", "MigrationAccountRow", "MigrationAccountPartyRow",
        "MigrationAccountInstallmentRow", "MigrationGmfExemptionRow", "MigrationTaxYearRow",
        // US6 (T169): la del CDAT.
        "MigrationTermDepositRow",
        // US11 (T187): la de bloqueos y embargos.
        "MigrationHoldRow",
    ];

    /// <summary>Hechos (H): DT §2.3, C42, C44, C112 y data-model partes 1 y 2.</summary>
    private static readonly string[] Hechos =
    [
        "CreditLineVersion", "CreditLineRateBand", "CreditLineDocumentRequirement", "CreditLineInsurance", "CreditLineCollateralRule",
        "CommercialTermEquivalence", "ContributionBalanceLoad", "CreditApplicationCheck", "CreditScore", "ScreeningResult", "BureauQuery",
        "CreditLimit", "DebtorBlock", "PrepaymentPreference", "AttributionMatrix", "AttributionLevel", "AttributionRule", "CreditDecision",
        "GoverningBodyRuleVersion", "SessionVote", "CaseParticipation", "ConnectedGroup", "ConnectedGroupMember", "LoanTermsVersion",
        "RepaymentScheduleVersion", "RepaymentInstallment", "LoanTransaction", "LoanDailyPosition", "LoanOperation",
        "DisbursementOrderResult", "DisbursementFile", "DisbursementFileLine", "InstallmentNonDeduction",
        "LendingInboundMessageEvaluation", "AccountingFactPart", "DebtorMessage", "DebtorMessageEvent", "PriorNotice",
        "IssuedLendingDocument", "IssuedDocumentLoan", "IssuedDocumentEvent", "MigrationLoadDifference", "ProvisionalCollection",
        "LendingActivation", "MonthlyClose", "MonthlyCloseLoan", "MonthlyCloseGeneralImpairment", "MonthlyCloseReopening", "LoanArrearsHistoryEntry",
        "RiskPolicyVersion", "PayrollDeductionFileLine", "PayrollDeductionResult", "NonDeductionFollowUp",
        "CollectionStageHistoryEntry", "CollectorAssignment", "CollectionAction", "PaymentPromise", "PaymentAgreementInstallment", "LegalProcessAction",
        "RegulatoryFileLine", "ImpairmentModelElection", "ArrearsHistoryLoad",
        // Feature 014 (T021–T025): el hecho primario de la cuenta, su libro y su posición.
        "MemberAccountOperation", "MemberAccountTransaction", "MemberAccountDailyPosition",
        // Feature 014, US1 (T060–T062): las versiones de las líneas de captación y de aportes y sus tramos.
        "DepositLineVersion", "DepositLineRateTier", "ContributionLineVersion",
        // Feature 014, US3 (T081): la cuota exigible y lo no descontado.
        "AccountInstallment", "AccountInstallmentNonDeduction", "IssuedDocumentAccount",
        // Feature 014, US10 (T096): las líneas de la liquidación del retiro.
        "MembershipWithdrawalLine",
        // Feature 014, US12 (T202): las líneas de la decisión de asamblea.
        "AssemblyDistributionLine",
        // Feature 014, respuesta 90 C (E261): la base de los retornos que sube la cooperativa.
        "AssemblyReturnBase",
        // Feature 014, US4 (T110, T114): el GMF del mes y el catálogo de cargos de la versión.
        "GmfMonthlyUsage", "DepositLineCharge",
        // Feature 014, T091 (D36): la cuota de cuenta del archivo de un grupo, su resultado y la fila de la respuesta que no se aplicó.
        "PayrollDeductionFileAccountLine", "PayrollDeductionAccountResult", "PayrollDeductionUnmatchedLine",
        // Feature 014, US5 (T130): la liquidación de intereses y la retención acumulada.
        "InterestSettlement", "WithholdingAccumulation",
        // Feature 014, US6 (T169): las condiciones selladas, el plan del CDAT y las reglas de tasa negociada.
        "MemberAccountTermsVersion", "TermDepositScheduleItem", "NegotiatedRateRule",
        // Feature 014, US11 (T187): la respuesta al juzgado.
        "GarnishmentResponse",
        // Feature 014, US15 (T215): la foto del fondo de liquidez.
        "LiquidityFundSnapshot",
        .. Preparacion,
    ];

    /// <summary>Sin diferencias de auditoría (T67, C46).</summary>
    private static readonly string[] SinDiff =
        ["LoanDailyPosition", "LoanControl", "DebtorControl", "AccountingFactPart", "LendingProcessRunError", "MonthlyCloseLoan", "LoanArrearsHistoryEntry",
            "PayrollDeductionFileLine", "RegulatoryFileLine", "ArrearsHistoryLoad", "MemberAccountDailyPosition", "MemberAccountControl", "GmfMonthlyUsage",
            "PayrollDeductionFileAccountLine", "WithholdingAccumulation", .. Preparacion];

    /// <summary><c>bigint</c> (data-model §0.1, C43, §26.2, §29.3).</summary>
    private static readonly string[] Largas =
    [
        "LoanOperation", "LoanTransaction", "LoanDailyPosition", "RepaymentInstallment", "DebtorMessageEvent", "RegulatoryFileLine",
        "MigrationInstallmentRow", "MigrationBalanceRow", "MigrationArrearsHistoryRow",
        "MemberAccountOperation", "MemberAccountTransaction", "MemberAccountDailyPosition", "AccountInstallment", "GmfMonthlyUsage",
        "WithholdingAccumulation", "MigrationAccountInstallmentRow",
    ];

    /// <summary>Controles y documentos con estado cuyos únicos tampoco filtran por borrado lógico (data-model §0.1).</summary>
    private static readonly string[] SinBajaLogica = ["Loan", "LoanControl", "DebtorControl", "LendingSetup", "MemberAccountControl"];

    public static TheoryData<bool> Motores => new() { true, false };

    private static List<IEntityType> DeCartera(IModel modelo) => modelo.GetEntityTypes()
        .Where(e => e.ClrType.Namespace?.StartsWith("IngenIA365ERP.Domain.Entities.Lending", StringComparison.Ordinal) == true)
        .ToList();

    [Theory]
    [MemberData(nameof(Motores))]
    public void Cada_entidad_mapea_a_una_tabla_de_CarteraNucleo_y_estan_todas(bool postgres)
    {
        var tablas = DeCartera(Modelo(postgres)).Select(e => e.GetTableName()).ToList();

        tablas.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(Tablas);
        Tablas.Should().HaveCount(184,
            "74 de negocio + 17 de preparación (data-model §14) + 2 del grupo conectado (C112) + 7 de CarteraRiesgo (US9) + 8 de CarteraLibranza (K2)"
            + " + 9 de CarteraNovedades (K3) + 11 de CarteraCobranza (K4) + 2 de CarteraInformes (K5) + 2 de CarteraPerdidaEsperada (K6a)"
            + " + 7 del libro de cuentas de la feature 014 (K7, CaptacionesAportes) + 3 de las versiones de líneas de captación y aportes (US1)"
            + " + 1 de la cuenta exenta del GMF (US2) + 4 de las cuotas exigibles, las solicitudes de aportes y las cuentas de un documento (US3)"
            + " + 2 de los grupos de archivo y las equivalencias de pagaduría (US3) + 2 del retiro del asociado y su liquidación (US10)"
            + " + 5 del GMF del mes, los cargos, sus exoneraciones y el débito automático (US4)"
            + " + 3 del archivo de pagaduría por grupo: cuotas de cuentas, sus resultados y lo no aplicado de la respuesta (T091) + 2 de la liquidación de intereses y la retención acumulada (US5)"
            + " + 7 de preparación de la migración de aportes y captaciones (US9) + 5 del CDAT: condiciones selladas, plan, reglas y solicitudes de tasa negociada y su fila de migración (US6)"
            + " + 4 de bloqueos, embargos, respuestas al juzgado y su fila de migración (US11) + 3 de la decisión de asamblea, sus líneas y la base de los retornos por plantilla (US12, E261) + 2 del fallecimiento y los no reclamados (US14) + 2 de la foto del fondo de liquidez y lo constituido (US15)");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Cada_entidad_tiene_PublicId_unico_filtro_de_baja_y_FK_Restrict(bool postgres)
    {
        foreach (var e in DeCartera(Modelo(postgres)))
        {
            var tabla = e.GetTableName();
            e.GetIndexes().Should().Contain(i => i.IsUnique && i.GetDatabaseName() == $"UK_{tabla}_PublicId"
                && i.Properties.Count == 1 && i.Properties[0].Name == nameof(BaseEntity.PublicId), tabla);
            e.GetDeclaredQueryFilters().Should().NotBeEmpty($"{tabla} filtra el borrado lógico (Principio VII)");
            e.GetForeignKeys().Should().OnlyContain(fk => fk.DeleteBehavior == DeleteBehavior.Restrict, $"{tabla}: nada en cascada");
        }
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Ninguna_FK_sale_hacia_Contabilidad_Comercial_Nomina_ni_Ahorros(bool postgres)
    {
        var prohibidas = DeCartera(Modelo(postgres))
            .SelectMany(e => e.GetForeignKeys().Select(fk => (Tabla: e.GetTableName()!, Destino: fk.PrincipalEntityType)))
            .Where(x => x.Destino.GetTableName() is { } t && (t.StartsWith("ACC_") || t.StartsWith("INV_") || t.StartsWith("PAY_"))
                || x.Destino.ClrType.Namespace?.Contains(".Savings", StringComparison.Ordinal) == true)
            .Select(x => $"{x.Tabla} → {x.Destino.GetTableName()}")
            .ToList();

        prohibidas.Should().BeEmpty("lo que llega de otros módulos se guarda como dato (data-model §0.1, FR-008)");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Los_hechos_de_alto_volumen_son_bigint(bool postgres)
    {
        var largas = DeCartera(Modelo(postgres)).Where(e => e.FindPrimaryKey()!.Properties[0].ClrType == typeof(long))
            .Select(e => e.ClrType.Name).ToList();

        largas.Should().BeEquivalentTo(Largas);
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Ningun_nombre_de_indice_pasa_de_63_caracteres(bool postgres)
    {
        var largos = DeCartera(Modelo(postgres)).SelectMany(e => e.GetIndexes())
            .Select(i => i.GetDatabaseName()!)
            .Where(n => n.Length > 63)
            .ToList();

        largos.Should().BeEmpty("PostgreSQL trunca los identificadores a 63 caracteres (data-model §0.13)");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Los_unicos_de_hechos_y_controles_no_filtran_por_baja_logica(bool postgres)
    {
        var sinFiltro = Hechos.Concat(SinBajaLogica).ToHashSet();
        var filtrados = DeCartera(Modelo(postgres))
            .Where(e => sinFiltro.Contains(e.ClrType.Name))
            .SelectMany(e => e.GetIndexes().Where(i => i.IsUnique && (i.GetFilter() ?? string.Empty).Contains("IsDeleted"))
                .Select(i => $"{e.GetTableName()}.{i.GetDatabaseName()}"))
            .ToList();

        filtrados.Should().BeEmpty("hechos, proyecciones, controles y créditos nunca se dan de baja (data-model §0.1)");
    }

    [Fact]
    public void Las_marcas_de_hecho_y_de_auditoria_son_las_fijadas()
    {
        var tipos = typeof(Domain.Entities.Lending.Loans.Loan).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Namespace?.StartsWith("IngenIA365ERP.Domain.Entities.Lending", StringComparison.Ordinal) == true)
            .ToList();

        tipos.Where(t => typeof(IHechoInmutable).IsAssignableFrom(t)).Select(t => t.Name).Should().BeEquivalentTo(Hechos);
        tipos.Where(t => t.IsDefined(typeof(SinDiffDeAuditoriaAttribute), inherit: false)).Select(t => t.Name).Should().BeEquivalentTo(SinDiff);
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void La_activacion_y_el_setup_no_forman_ciclo_y_las_partes_apuntan_al_cierre(bool postgres)
    {
        var modelo = Modelo(postgres);
        var setup = modelo.FindEntityType(typeof(Domain.Entities.Lending.Configuration.LendingSetup))!;
        // Feature 014 (T053): la de créditos y, desde la 014, la de captaciones y la de aportes, las tres hacia LND_Activations.
        setup.GetForeignKeys().Where(fk => fk.PrincipalEntityType.GetTableName() == "LND_Activations")
            .Select(fk => fk.Properties.Single().Name).Should().BeEquivalentTo(["ActivationId", "DepositsActivationId", "ContributionsActivationId"]);
        modelo.FindEntityType(typeof(Domain.Entities.Lending.Migration.LendingActivation))!.GetForeignKeys()
            .Should().NotContain(fk => fk.PrincipalEntityType.GetTableName() == "LND_Setup");

        var partes = modelo.FindEntityType(typeof(Domain.Entities.Lending.Integration.AccountingFactPart))!;
        partes.FindProperty("MonthlyCloseId").Should().NotBeNull();
        // US9 (T0697, C66): CarteraRiesgo crea las FK hacia el cierre y su reapertura.
        partes.GetForeignKeys().Should().Contain(fk => fk.Properties.Single().Name == "MonthlyCloseId" && fk.PrincipalEntityType.GetTableName() == "LND_MonthlyCloses")
            .And.Contain(fk => fk.Properties.Single().Name == "MonthlyCloseReopeningId" && fk.PrincipalEntityType.GetTableName() == "LND_MonthlyCloseReopenings");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Las_columnas_de_K3_en_tablas_de_K1_apuntan_a_sus_tablas(bool postgres)
    {
        // K3 (T0876; data-model §14, parte 3): cinco columnas nacen con CarteraNovedades en tablas de K1.
        var modelo = Modelo(postgres);
        void Apunta(Type entidad, string columna, string tabla) =>
            modelo.FindEntityType(entidad)!.GetForeignKeys()
                .Should().Contain(fk => fk.Properties.Single().Name == columna && fk.PrincipalEntityType.GetTableName() == tabla, $"{entidad.Name}.{columna}");

        Apunta(typeof(Domain.Entities.Lending.Loans.InsolvencyProceeding), "AgreementNoveltyId", "LND_LoanNovelties");
        Apunta(typeof(Domain.Entities.Lending.Loans.LoanTermsVersion), "NoveltyId", "LND_LoanNovelties");
        Apunta(typeof(Domain.Entities.Lending.Operations.LoanOperation), "NoveltyId", "LND_LoanNovelties");
        Apunta(typeof(Domain.Entities.Lending.Operations.LoanOperation), "WaiverId", "LND_Waivers");
        Apunta(typeof(Domain.Entities.Lending.Operations.LoanOperation), "WriteOffBatchItemId", "LND_WriteOffBatchItems");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void La_columna_de_K6a_del_historial_apunta_a_la_carga_complementaria(bool postgres)
    {
        // K6a (T1032): la columna nació nula en CarteraRiesgo; su FK e índice nacen con CarteraPerdidaEsperada.
        var historial = Modelo(postgres).FindEntityType(typeof(Domain.Entities.Lending.Risk.LoanArrearsHistoryEntry))!;
        historial.GetForeignKeys().Should().Contain(fk => fk.Properties.Single().Name == "ArrearsHistoryLoadId"
            && fk.PrincipalEntityType.GetTableName() == "LND_ArrearsHistoryLoads");
        historial.GetIndexes().Should().Contain(i => i.GetDatabaseName() == "IX_LND_LoanArrearsHistory_ArrearsHistoryLoadId");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Las_columnas_de_K6b_son_nulas_en_UVR_o_con_su_defecto_en_pesos(bool postgres)
    {
        // K6b (T1048, T1059; CarteraVivienda): ninguna tabla nueva; la unidad del capital nace en pesos para lo anterior y los valores en UVR son
        // nulos fuera de un crédito en UVR, con el alias ValorUvr (18,4).
        var modelo = Modelo(postgres);
        foreach (var tipo in new[] { typeof(Domain.Entities.Lending.Configuration.CreditLineVersion), typeof(Domain.Entities.Lending.Loans.LoanTermsVersion) })
        {
            var unidad = modelo.FindEntityType(tipo)!.FindProperty("DenominationUnit")!;
            unidad.IsNullable.Should().BeFalse();
            unidad.GetDefaultValue().Should().Be(Domain.Enums.Lending.DenominationUnit.Pesos, tipo.Name);
        }
        foreach (var (tipo, columnas) in new (Type, string[])[]
                 {
                     (typeof(Domain.Entities.Lending.Schedules.RepaymentInstallment),
                         ["OpeningPrincipalUvr", "PrincipalUvr", "InterestUvr", "InsuranceUvr", "TotalUvr", "ClosingPrincipalUvr"]),
                     (typeof(Domain.Entities.Lending.Schedules.RepaymentScheduleVersion), ["UvrValue"]),
                     (typeof(Domain.Entities.Lending.Transactions.LoanTransaction), ["UvrAmount", "UvrValue"]),
                     (typeof(Domain.Entities.Lending.Projections.LoanDailyPosition), ["UvrValue", "PrincipalUvr", "PayoffAmountUvr"]),
                 })
            foreach (var columna in columnas)
            {
                var p = modelo.FindEntityType(tipo)!.FindProperty(columna)!;
                (p.IsNullable, p.GetPrecision(), p.GetScale()).Should().Be((true, 18, 4), $"{tipo.Name}.{columna}");
            }
        modelo.FindEntityType(typeof(Domain.Entities.Lending.Origination.CreditApplication))!.FindProperty("IsSocialHousing")!.IsNullable.Should().BeTrue();
    }

    private static IModel Modelo(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres
            ? builder.UseNpgsql("Host=localhost;Database=x;Username=y;Password=z")
            : builder.UseSqlServer("Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True");
        using var db = new ApplicationDbContext(builder.Options);
        return db.Model;
    }
}
