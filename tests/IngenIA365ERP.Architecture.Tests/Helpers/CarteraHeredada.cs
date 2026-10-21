namespace IngenIA365ERP.Architecture.Tests.Helpers;

/// <summary>
/// Feature 013 (data-model parte 3 §13.1; T2, T3, T86): la clasificación de las 93 tablas <c>LND_</c> heredadas, en un solo
/// sitio para las pruebas que vigilan el retiro (<c>LaCarteraHeredadaNoVuelve</c>, <c>NingunaTablaConservadaApuntaALaCarteraRetirada</c>,
/// <c>PrincipioXII_MigracionesDestructivas</c>). Es la lista del diseño, no la del modelo: el modelo es lo que se compara contra ella.
///
/// <para>
/// Feature 014 (T009, R28, R29): <b>ya no hay conservado</b>. Las 38 que la 013 conservó (ahorros, aportes, caja, SIPLA, zonas y
/// bienestar) y las 7 <c>CDT_*</c> las retira <c>RetiroDeCaptacionesHeredadas</c>: son las 45 de
/// <see cref="RetiradasPorCaptaciones"/>. Los nombres heredados que ninguna tabla nueva puede reutilizar son ahora 100.
/// </para>
/// </summary>
internal static class CarteraHeredada
{
    /// <summary>Las 55 tablas de crédito que retira <c>RetiroDeLaCarteraHeredada</c>, con la entidad que las mapeaba.</summary>
    public static readonly (string Tabla, string Entidad)[] Retiradas =
    [
        ("LND_AccrualEntries", "AccrualEntry"),
        ("LND_AccrualPeriods", "AccrualPeriod"),
        ("LND_ApplicationAssets", "ApplicationAsset"),
        ("LND_ApplicationCodebtors", "ApplicationCodebtor"),
        ("LND_ApplicationExtras", "ApplicationExtra"),
        ("LND_ApplicationReferences", "ApplicationReference"),
        ("LND_ApplicationStatuses", "ApplicationStatus"),
        ("LND_CollectionCases", "CollectionCase"),
        ("LND_CollectionDateEntries", "CollectionDateEntry"),
        ("LND_CollectionMasters", "CollectionMaster"),
        ("LND_CollectionNoticeDetails", "CollectionNoticeDetail"),
        ("LND_CollectionNoticeParams", "CollectionNoticeParam"),
        ("LND_CollectionNotices", "CollectionNotice"),
        ("LND_CollectionPeriods", "CollectionPeriod"),
        ("LND_CreditLineAudit", "CreditLineAudit"),
        ("LND_CreditLineParameters", "CreditLineParameter"),
        ("LND_CreditParameters", "CreditParameter"),
        ("LND_DeductionValues", "DeductionValue"),
        ("LND_DefaultLiquidations", "DefaultLiquidation"),
        ("LND_DefaultRecords", "DefaultRecord"),
        ("LND_ExtraPaymentBalances", "ExtraPaymentBalance"),
        ("LND_ExtraPayments", "ExtraPayment"),
        ("LND_Guarantees", "Guarantee"),
        ("LND_HousingApplicationParams", "HousingApplicationParam"),
        ("LND_HousingParameters", "HousingParameter"),
        ("LND_InsuranceBeneficiaries", "InsuranceBeneficiary"),
        ("LND_InsurancePolicies", "InsurancePolicy"),
        ("LND_InterestRates", "InterestRate"),
        ("LND_InvoiceDetails", "InvoiceDetail"),
        ("LND_InvoiceLineItems", "InvoiceLineItem"),
        ("LND_InvoiceMasters", "InvoiceMaster"),
        ("LND_LoanApplications", "LoanApplication"),
        ("LND_LoanPortfolios", "LoanPortfolio"),
        ("LND_LoanRestructurings", "LoanRestructuring"),
        ("LND_OnlineQueries", "OnlineQuery"),
        ("LND_PayrollDeductionConcepts", "PayrollDeductionConcept"),
        ("LND_PayrollDeductionEntries", "PayrollDeductionEntry"),
        ("LND_PayrollDeductionPeriods", "PayrollDeductionPeriod"),
        ("LND_PayrollDeductions", "PayrollDeduction"),
        ("LND_PendingInstallments", "PendingInstallment"),
        ("LND_PeriodicityParameters", "PeriodicityParameter"),
        ("LND_PortfolioAccounts", "PortfolioAccount"),
        ("LND_PortfolioBalances", "PortfolioBalance"),
        ("LND_PortfolioClassifications", "PortfolioClassification"),
        ("LND_PortfolioInvoices", "PortfolioInvoice"),
        ("LND_PreviousInstallments", "PreviousInstallment"),
        ("LND_ProvisionParameters", "ProvisionParameter"),
        ("LND_RatesByTerm", "RateByTerm"),
        ("LND_RecreationApplications", "RecreationApplication"),
        ("LND_RiskAssessments", "RiskAssessment"),
        ("LND_ScoringParameters", "ScoringParameter"),
        ("LND_ScoringRanges", "ScoringRange"),
        ("LND_ShortLongTermPortfolio", "ShortLongTermPortfolio"),
        ("LND_TermRates", "TermRate"),
        ("LND_Transactions", "LendingTransaction"),
    ];

    /// <summary>
    /// Las 38 tablas <c>LND_</c> que la 013 conservó sin cambio de <c>ToTable</c> (su entidad vivía en <c>Domain.Entities.Savings</c>);
    /// la 014 las retira con <c>RetiroDeCaptacionesHeredadas</c>.
    /// </summary>
    public static readonly (string Tabla, string Entidad)[] Conservadas =
    [
        ("LND_SavingsAccounts", "SavingsAccount"),
        ("LND_SavingsParameters", "SavingsParameter"),
        ("LND_DepositAccounts", "DepositAccount"),
        ("LND_DepositEntries", "DepositEntry"),
        ("LND_DepositAudit", "DepositAudit"),
        ("LND_DepositSeals", "DepositSeal"),
        ("LND_DepositSignatures", "DepositSignature"),
        ("LND_Cashiers", "Cashier"),
        ("LND_CashBases", "CashBase"),
        ("LND_Checkbooks", "Checkbook"),
        ("LND_CheckClearing", "CheckClearing"),
        ("LND_ContributionReductions", "ContributionReduction"),
        ("LND_ContributionReductionParams", "ContributionReductionParam"),
        ("LND_AssociateWithdrawals", "AssociateWithdrawal"),
        ("LND_WithdrawalStatuses", "WithdrawalStatus"),
        ("LND_SiplaParameters", "SiplaParameter"),
        ("LND_SiplaGroupParameters", "SiplaGroupParameter"),
        ("LND_UnusualTransactions", "UnusualTransaction"),
        ("LND_UnusualTransactionEntries", "UnusualTransactionEntry"),
        ("LND_MoneyLaunderingDeclarations", "MoneyLaunderingDeclaration"),
        ("LND_Minutes", "Minute"),
        ("LND_MinuteAttendees", "MinuteAttendee"),
        ("LND_Blacklist", "BlacklistEntry"),
        ("LND_TransactionCodes", "TransactionCode"),
        ("LND_Documents", "LendingDocument"),
        ("LND_ActivityEnrollments", "ActivityEnrollment"),
        ("LND_AssociateActivities", "AssociateActivity"),
        ("LND_AssociateDiseases", "AssociateDisease"),
        ("LND_BiometricRecords", "BiometricRecord"),
        ("LND_PersonAssets", "PersonAsset"),
        ("LND_Subsidies", "Subsidy"),
        ("LND_AuxiliaryApplications", "AuxiliaryApplication"),
        ("LND_AuxiliaryApplicationLines", "AuxiliaryApplicationLine"),
        ("LND_AuxAppInstallments", "AuxAppInstallment"),
        ("LND_AuxAppInstallmentBeneficiaries", "AuxAppInstallmentBeneficiary"),
        ("LND_Zones", "Zone"),
        ("LND_SubZones", "SubZone"),
        ("LND_ZoneTypes", "ZoneType"),
    ];

    /// <summary>Las 7 tablas del CDT heredado (su entidad vivía en <c>Domain.Entities.CDT</c>), que la 014 retira con las 38.</summary>
    public static readonly (string Tabla, string Entidad)[] DelCdt =
    [
        ("CDT_AssociateReferences", "CdtAssociateReference"),
        ("CDT_Audit", "CdtAudit"),
        ("CDT_CertificateEntries", "CertificateEntry"),
        ("CDT_Certificates", "Certificate"),
        ("CDT_ParameterAudit", "CdtParameterAudit"),
        ("CDT_Parameters", "CdtParameter"),
        ("CDT_RatesByTerm", "CdtRateByTerm"),
    ];

    /// <summary>Feature 014 (R28): las 45 que retira <c>RetiroDeCaptacionesHeredadas</c> (las 38 conservadas y las 7 del CDT).</summary>
    public static IEnumerable<(string Tabla, string Entidad)> RetiradasPorCaptaciones => Conservadas.Concat(DelCdt);

    /// <summary>Los espacios de las entidades retiradas por la 014: ninguno vuelve.</summary>
    public static readonly string[] EspaciosRetiradosPorCaptaciones = ["IngenIA365ERP.Domain.Entities.Savings", "IngenIA365ERP.Domain.Entities.CDT"];

    /// <summary>Los 100 nombres heredados (93 <c>LND_</c> y 7 <c>CDT_</c>): ninguna tabla nueva puede reutilizar uno (T2, R124; 014 R28).</summary>
    public static IEnumerable<string> NombresHeredados =>
        Retiradas.Select(r => r.Tabla).Concat(RetiradasPorCaptaciones.Select(c => c.Tabla));
}
