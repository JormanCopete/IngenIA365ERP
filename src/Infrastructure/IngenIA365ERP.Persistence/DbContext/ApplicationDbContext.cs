using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Lending;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.CDT;
using IngenIA365ERP.Domain.Entities.Debit;
using IngenIA365ERP.Domain.Entities.Treasury;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Entities.Web;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Exceptions;
using IngenIA365ERP.Persistence.Configurations.Common;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.DbContext;

public class ApplicationDbContext : Microsoft.EntityFrameworkCore.DbContext, IApplicationDbContext
{
    private readonly ErpTenantInfo? _tenantInfo;
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ErpTenantInfo? tenantInfo = null,
        ICurrentUserService? currentUserService = null,
        ISenalDeMensajes? senalDeMensajes = null) : base(options)
    {
        _tenantInfo = tenantInfo;
        _currentUserService = currentUserService;
        if (senalDeMensajes is not null) AvisarMensajesGuardados(senalDeMensajes);
    }

    /// <summary>
    /// Feature 012 (T10, T078): cuando un guardado incluyó mensajes de integración nuevos, avisa a
    /// <see cref="ISenalDeMensajes"/> para despertar al despachador (I2). Los <c>PublicId</c> se toman en
    /// <c>SavingChanges</c> —después del guardado ya no están <c>Added</c>— y se avisan sólo en
    /// <c>SavedChanges</c>: un guardado que falla no despierta a nadie. El aviso es del <c>SaveChanges</c>, no del
    /// commit de una transacción explícita; el sondeo del despachador cubre ese hueco.
    /// </summary>
    private void AvisarMensajesGuardados(ISenalDeMensajes senal)
    {
        List<Guid>? porAvisar = null;
        SavingChanges += (_, _) =>
        {
            porAvisar = ChangeTracker.Entries<IntegrationMessage>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity.PublicId)
                .ToList();
        };
        SavedChanges += (_, _) =>
        {
            if (porAvisar is { Count: > 0 }) senal.Avisar(porAvisar);
            porAvisar = null;
        };
        SaveChangesFailed += (_, _) => porAvisar = null;
    }

    /// <summary>
    /// Esquema del tenant activo — participa de la clave de cache del modelo
    /// (feature 004, SchemaModelCacheKeyFactory).
    /// </summary>
    public string? TenantSchema => _tenantInfo?.Schema;

    // === Core (42) ===
    public DbSet<Person> People => Set<Person>();
    public DbSet<Associate> Associates => Set<Associate>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankFileFormat> BankFileFormats => Set<BankFileFormat>();
    public DbSet<BankFileFormatField> BankFileFormatFields => Set<BankFileFormatField>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<Reference> References => Set<Reference>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Spouse> Spouses => Set<Spouse>();
    public DbSet<PersonFinancial> PeopleFinancial => Set<PersonFinancial>();
    public DbSet<AssociateCategory> AssociateCategories => Set<AssociateCategory>();
    public DbSet<CommitteeMember> CommitteeMembers => Set<CommitteeMember>();
    public DbSet<EmployerCompany> EmployerCompanies => Set<EmployerCompany>();
    public DbSet<WithdrawalReason> WithdrawalReasons => Set<WithdrawalReason>();
    public DbSet<Advisor> Advisors => Set<Advisor>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<CulturalActivity> CulturalActivities => Set<CulturalActivity>();
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<RecreationalEvent> RecreationalEvents => Set<RecreationalEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDeliveryFailure> NotificationDeliveryFailures => Set<NotificationDeliveryFailure>();
    public DbSet<HabeasDataPolicyVersion> HabeasDataPolicyVersions => Set<HabeasDataPolicyVersion>();
    public DbSet<HabeasDataConsent> HabeasDataConsents => Set<HabeasDataConsent>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<ExternalEntity> ExternalEntities => Set<ExternalEntity>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Profession> Professions => Set<Profession>();
    public DbSet<Disease> Diseases => Set<Disease>();
    public DbSet<Agreement> Agreements => Set<Agreement>();
    public DbSet<ActivityProgram> ActivityPrograms => Set<ActivityProgram>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<InvoiceParameter> InvoiceParameters => Set<InvoiceParameter>();
    public DbSet<LegalAdvisor> LegalAdvisors => Set<LegalAdvisor>();
    public DbSet<ListParameter> ListParameters => Set<ListParameter>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<PaymentMethodCheck> PaymentMethodChecks => Set<PaymentMethodCheck>();
    public DbSet<PensionSeveranceParam> PensionSeveranceParams => Set<PensionSeveranceParam>();
    public DbSet<Sequence> Sequences => Set<Sequence>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // === Accounting (feature 009: 29 entidades; las 33 heredadas se retiraron) ===
    public DbSet<AccountingSetup> AccountingSetups => Set<AccountingSetup>();
    public DbSet<AccountCatalog> AccountCatalogs => Set<AccountCatalog>();
    public DbSet<AccountCatalogEntry> AccountCatalogEntries => Set<AccountCatalogEntry>();
    public DbSet<FinancialStatementItem> FinancialStatementItems => Set<FinancialStatementItem>();
    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();
    public DbSet<AccountTaxRate> AccountTaxRates => Set<AccountTaxRate>();
    public DbSet<VoucherType> VoucherTypes => Set<VoucherType>();
    public DbSet<CrossDocumentType> CrossDocumentTypes => Set<CrossDocumentType>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<AccountingDocument> AccountingDocuments => Set<AccountingDocument>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<BankStatementColumnMap> BankStatementColumnMaps => Set<BankStatementColumnMap>();
    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<WithholdingCertificate> WithholdingCertificates => Set<WithholdingCertificate>();
    public DbSet<WithholdingCertificateLine> WithholdingCertificateLines => Set<WithholdingCertificateLine>();
    public DbSet<TaxForm> TaxForms => Set<TaxForm>();
    public DbSet<TaxFormLine> TaxFormLines => Set<TaxFormLine>();
    public DbSet<ExogenousFormat> ExogenousFormats => Set<ExogenousFormat>();
    public DbSet<ExogenousConcept> ExogenousConcepts => Set<ExogenousConcept>();
    public DbSet<ExogenousConceptAccount> ExogenousConceptAccounts => Set<ExogenousConceptAccount>();
    public DbSet<ExogenousRun> ExogenousRuns => Set<ExogenousRun>();
    public DbSet<ExogenousRunLine> ExogenousRunLines => Set<ExogenousRunLine>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<FixedAssetInstallment> FixedAssetInstallments => Set<FixedAssetInstallment>();
    public DbSet<AssetRun> AssetRuns => Set<AssetRun>();

    // === Lending (93) ===
    public DbSet<LoanPortfolio> LoanPortfolios => Set<LoanPortfolio>();
    public DbSet<LendingTransaction> LendingTransactions => Set<LendingTransaction>();
    public DbSet<PendingInstallment> PendingInstallments => Set<PendingInstallment>();
    public DbSet<CreditLineParameter> CreditLineParameters => Set<CreditLineParameter>();
    public DbSet<CreditParameter> CreditParameters => Set<CreditParameter>();
    public DbSet<DefaultRecord> DefaultRecords => Set<DefaultRecord>();
    public DbSet<DefaultLiquidation> DefaultLiquidations => Set<DefaultLiquidation>();
    public DbSet<ExtraPayment> ExtraPayments => Set<ExtraPayment>();
    public DbSet<ExtraPaymentBalance> ExtraPaymentBalances => Set<ExtraPaymentBalance>();
    public DbSet<Guarantee> Guarantees => Set<Guarantee>();
    public DbSet<CollectionCase> CollectionCases => Set<CollectionCase>();
    public DbSet<CollectionMaster> CollectionMasters => Set<CollectionMaster>();
    public DbSet<CollectionNotice> CollectionNotices => Set<CollectionNotice>();
    public DbSet<CollectionNoticeDetail> CollectionNoticeDetails => Set<CollectionNoticeDetail>();
    public DbSet<CollectionNoticeParam> CollectionNoticeParams => Set<CollectionNoticeParam>();
    public DbSet<CollectionPeriod> CollectionPeriods => Set<CollectionPeriod>();
    public DbSet<CollectionDateEntry> CollectionDateEntries => Set<CollectionDateEntry>();
    public DbSet<SavingsAccount> SavingsAccounts => Set<SavingsAccount>();
    public DbSet<SavingsParameter> SavingsParameters => Set<SavingsParameter>();
    public DbSet<DepositAccount> DepositAccounts => Set<DepositAccount>();
    public DbSet<DepositEntry> DepositEntries => Set<DepositEntry>();
    public DbSet<DepositAudit> DepositAudits => Set<DepositAudit>();
    public DbSet<DepositSeal> DepositSeals => Set<DepositSeal>();
    public DbSet<DepositSignature> DepositSignatures => Set<DepositSignature>();
    public DbSet<PayrollDeduction> PayrollDeductions => Set<PayrollDeduction>();
    public DbSet<PayrollDeductionConcept> PayrollDeductionConcepts => Set<PayrollDeductionConcept>();
    public DbSet<PayrollDeductionEntry> PayrollDeductionEntries => Set<PayrollDeductionEntry>();
    public DbSet<PayrollDeductionPeriod> PayrollDeductionPeriods => Set<PayrollDeductionPeriod>();
    public DbSet<PortfolioClassification> PortfolioClassifications => Set<PortfolioClassification>();
    public DbSet<PortfolioAccount> PortfolioAccounts => Set<PortfolioAccount>();
    public DbSet<PortfolioBalance> PortfolioBalances => Set<PortfolioBalance>();
    public DbSet<PortfolioInvoice> PortfolioInvoices => Set<PortfolioInvoice>();
    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();
    public DbSet<ApplicationAsset> ApplicationAssets => Set<ApplicationAsset>();
    public DbSet<ApplicationCodebtor> ApplicationCodebtors => Set<ApplicationCodebtor>();
    public DbSet<ApplicationExtra> ApplicationExtras => Set<ApplicationExtra>();
    public DbSet<ApplicationReference> ApplicationReferences => Set<ApplicationReference>();
    public DbSet<ApplicationStatus> ApplicationStatuses => Set<ApplicationStatus>();
    public DbSet<LoanRestructuring> LoanRestructurings => Set<LoanRestructuring>();
    public DbSet<InsurancePolicy> InsurancePolicies => Set<InsurancePolicy>();
    public DbSet<InsuranceBeneficiary> InsuranceBeneficiaries => Set<InsuranceBeneficiary>();
    public DbSet<InterestRate> InterestRates => Set<InterestRate>();
    public DbSet<RateByTerm> RatesByTerm => Set<RateByTerm>();
    public DbSet<TermRate> TermRates => Set<TermRate>();
    public DbSet<PeriodicityParameter> PeriodicityParameters => Set<PeriodicityParameter>();
    public DbSet<DeductionValue> DeductionValues => Set<DeductionValue>();
    public DbSet<TransactionCode> TransactionCodes => Set<TransactionCode>();
    public DbSet<LendingDocument> LendingDocuments => Set<LendingDocument>();
    public DbSet<InvoiceMaster> InvoiceMasters => Set<InvoiceMaster>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<AccrualEntry> AccrualEntries => Set<AccrualEntry>();
    public DbSet<AccrualPeriod> AccrualPeriods => Set<AccrualPeriod>();
    public DbSet<ProvisionParameter> ProvisionParameters => Set<ProvisionParameter>();
    public DbSet<ShortLongTermPortfolio> ShortLongTermPortfolios => Set<ShortLongTermPortfolio>();
    public DbSet<CreditLineAudit> CreditLineAudits => Set<CreditLineAudit>();
    public DbSet<BiometricRecord> BiometricRecords => Set<BiometricRecord>();
    public DbSet<BlacklistEntry> BlacklistEntries => Set<BlacklistEntry>();
    public DbSet<MoneyLaunderingDeclaration> MoneyLaunderingDeclarations => Set<MoneyLaunderingDeclaration>();
    public DbSet<UnusualTransaction> UnusualTransactions => Set<UnusualTransaction>();
    public DbSet<UnusualTransactionEntry> UnusualTransactionEntries => Set<UnusualTransactionEntry>();
    public DbSet<SiplaParameter> SiplaParameters => Set<SiplaParameter>();
    public DbSet<SiplaGroupParameter> SiplaGroupParameters => Set<SiplaGroupParameter>();
    public DbSet<ScoringParameter> ScoringParameters => Set<ScoringParameter>();
    public DbSet<ScoringRange> ScoringRanges => Set<ScoringRange>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<Cashier> Cashiers => Set<Cashier>();
    public DbSet<CashBase> CashBases => Set<CashBase>();
    public DbSet<Checkbook> Checkbooks => Set<Checkbook>();
    public DbSet<CheckClearing> CheckClearings => Set<CheckClearing>();
    public DbSet<OnlineQuery> OnlineQueries => Set<OnlineQuery>();
    public DbSet<PersonAsset> PersonAssets => Set<PersonAsset>();
    public DbSet<PreviousInstallment> PreviousInstallments => Set<PreviousInstallment>();
    public DbSet<Minute> Minutes => Set<Minute>();
    public DbSet<MinuteAttendee> MinuteAttendees => Set<MinuteAttendee>();
    public DbSet<ContributionReduction> ContributionReductions => Set<ContributionReduction>();
    public DbSet<ContributionReductionParam> ContributionReductionParams => Set<ContributionReductionParam>();
    public DbSet<ActivityEnrollment> ActivityEnrollments => Set<ActivityEnrollment>();
    public DbSet<AssociateActivity> AssociateActivities => Set<AssociateActivity>();
    public DbSet<AssociateDisease> AssociateDiseases => Set<AssociateDisease>();
    public DbSet<AssociateWithdrawal> AssociateWithdrawals => Set<AssociateWithdrawal>();
    public DbSet<AuxiliaryApplication> LendingAuxiliaryApplications => Set<AuxiliaryApplication>();
    public DbSet<AuxiliaryApplicationLine> AuxiliaryApplicationLines => Set<AuxiliaryApplicationLine>();
    public DbSet<AuxAppInstallment> AuxAppInstallments => Set<AuxAppInstallment>();
    public DbSet<AuxAppInstallmentBeneficiary> AuxAppInstallmentBeneficiaries => Set<AuxAppInstallmentBeneficiary>();
    public DbSet<RecreationApplication> RecreationApplications => Set<RecreationApplication>();
    public DbSet<HousingApplicationParam> HousingApplicationParams => Set<HousingApplicationParam>();
    public DbSet<HousingParameter> HousingParameters => Set<HousingParameter>();
    public DbSet<Subsidy> Subsidies => Set<Subsidy>();
    public DbSet<WithdrawalStatus> WithdrawalStatuses => Set<WithdrawalStatus>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<ZoneType> ZoneTypes => Set<ZoneType>();
    public DbSet<SubZone> SubZones => Set<SubZone>();

    // === Payroll (27) ===
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayrollConcept> PayrollConcepts => Set<PayrollConcept>();
    public DbSet<PayrollPlanLiquidation> PayrollPlanLiquidations => Set<PayrollPlanLiquidation>();
    public DbSet<PayrollTransaction> PayrollTransactions => Set<PayrollTransaction>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<SalaryChange> SalaryChanges => Set<SalaryChange>();
    public DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<DirectDebit> DirectDebits => Set<DirectDebit>();
    public DbSet<SeveranceProvider> SeveranceProviders => Set<SeveranceProvider>();
    public DbSet<FamilyCompensationFund> FamilyCompensationFunds => Set<FamilyCompensationFund>();
    public DbSet<SeveranceHistory> SeveranceHistories => Set<SeveranceHistory>();
    public DbSet<VacationLiquidation> VacationLiquidations => Set<VacationLiquidation>();
    public DbSet<PreLiquidation> PreLiquidations => Set<PreLiquidation>();
    public DbSet<PreLiquidationResponse> PreLiquidationResponses => Set<PreLiquidationResponse>();
    public DbSet<EmployeeLiquidationMaster> EmployeeLiquidationMasters => Set<EmployeeLiquidationMaster>();
    public DbSet<EmployeeLiquidationDetail> EmployeeLiquidationDetails => Set<EmployeeLiquidationDetail>();
    public DbSet<PayrollAccountingEntry> PayrollAccountingEntries => Set<PayrollAccountingEntry>();
    public DbSet<ConceptAccount> ConceptAccounts => Set<ConceptAccount>();
    public DbSet<BookBalance> BookBalances => Set<BookBalance>();
    public DbSet<WithholdingCause> WithholdingCauses => Set<WithholdingCause>();
    public DbSet<WithholdingParameter> WithholdingParameters => Set<WithholdingParameter>();
    public DbSet<HealthInsuranceProvider> HealthInsuranceProviders => Set<HealthInsuranceProvider>();
    public DbSet<WorkRiskProvider> WorkRiskProviders => Set<WorkRiskProvider>();
    public DbSet<WorkRiskRate> WorkRiskRates => Set<WorkRiskRate>();
    public DbSet<PensionProvider> PensionProviders => Set<PensionProvider>();
    public DbSet<TaxCertificate> TaxCertificates => Set<TaxCertificate>();
    public DbSet<AutoContributionParam> AutoContributionParams => Set<AutoContributionParam>();

    // Payroll — feature 005 (novedades y liquidacion)
    public DbSet<PayrollPlan> PayrollPlans => Set<PayrollPlan>();
    public DbSet<PayrollConceptDefinition> PayrollConceptDefinitions => Set<PayrollConceptDefinition>();
    public DbSet<PayrollConceptDefinitionAccount> PayrollConceptDefinitionAccounts => Set<PayrollConceptDefinitionAccount>();
    public DbSet<PayrollLegalParameter> PayrollLegalParameters => Set<PayrollLegalParameter>();
    public DbSet<PayrollLegalParameterRange> PayrollLegalParameterRanges => Set<PayrollLegalParameterRange>();
    public DbSet<PayrollNovelty> PayrollNovelties => Set<PayrollNovelty>();
    public DbSet<PayrollRecurringNovelty> PayrollRecurringNovelties => Set<PayrollRecurringNovelty>();
    public DbSet<EmployeeWithholdingRate> EmployeeWithholdingRates => Set<EmployeeWithholdingRate>();
    public DbSet<EmployeeTaxDeduction> EmployeeTaxDeductions => Set<EmployeeTaxDeduction>();
    public DbSet<Domain.Entities.Payroll.Transactions.PayrollRun> PayrollRuns => Set<Domain.Entities.Payroll.Transactions.PayrollRun>();
    public DbSet<Domain.Entities.Payroll.Transactions.PayrollRunEmployee> PayrollRunEmployees => Set<Domain.Entities.Payroll.Transactions.PayrollRunEmployee>();
    public DbSet<Domain.Entities.Payroll.Transactions.PayrollRunLine> PayrollRunLines => Set<Domain.Entities.Payroll.Transactions.PayrollRunLine>();
    public DbSet<PayrollPayment> PayrollPayments => Set<PayrollPayment>();
    public DbSet<PayslipDelivery> PayslipDeliveries => Set<PayslipDelivery>();

    // === Payroll — feature 010 (entrega N1) ===
    public DbSet<CompanyPolicy> CompanyPolicies => Set<CompanyPolicy>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<EmployeeBenefitOpeningBalance> EmployeeBenefitOpeningBalances => Set<EmployeeBenefitOpeningBalance>();
    public DbSet<VacationMovement> VacationMovements => Set<VacationMovement>();
    public DbSet<TerminationReason> TerminationReasons => Set<TerminationReason>();
    public DbSet<EmploymentTermination> EmploymentTerminations => Set<EmploymentTermination>();
    public DbSet<SettlementDeduction> SettlementDeductions => Set<SettlementDeduction>();
    public DbSet<WithholdingRateCalculation> WithholdingRateCalculations => Set<WithholdingRateCalculation>();
    public DbSet<WithholdingRateCalculationMonth> WithholdingRateCalculationMonths => Set<WithholdingRateCalculationMonth>();
    public DbSet<SeveranceFundDeposit> SeveranceFundDeposits => Set<SeveranceFundDeposit>();
    public DbSet<Domain.Entities.Payroll.Transactions.BankDisbursementFile> BankDisbursementFiles => Set<Domain.Entities.Payroll.Transactions.BankDisbursementFile>();
    public DbSet<Domain.Entities.Payroll.Transactions.BankDisbursementFileLine> BankDisbursementFileLines => Set<Domain.Entities.Payroll.Transactions.BankDisbursementFileLine>();
    public DbSet<Domain.Entities.Payroll.Pila.PilaSettings> PilaSettings => Set<Domain.Entities.Payroll.Pila.PilaSettings>();
    public DbSet<Domain.Entities.Payroll.Transactions.PilaGeneration> PilaGenerations => Set<Domain.Entities.Payroll.Transactions.PilaGeneration>();
    public DbSet<Domain.Entities.Payroll.Transactions.PilaGenerationLine> PilaGenerationLines => Set<Domain.Entities.Payroll.Transactions.PilaGenerationLine>();
    public DbSet<Domain.Entities.Payroll.Pila.PilaIssue> PilaIssues => Set<Domain.Entities.Payroll.Pila.PilaIssue>();
    public DbSet<Domain.Entities.Payroll.ElectronicPayroll.ElectronicPayrollSettings> ElectronicPayrollSettings => Set<Domain.Entities.Payroll.ElectronicPayroll.ElectronicPayrollSettings>();
    public DbSet<Domain.Entities.Payroll.ElectronicPayroll.ElectronicPayrollNumberingRange> ElectronicPayrollNumberingRanges => Set<Domain.Entities.Payroll.ElectronicPayroll.ElectronicPayrollNumberingRange>();
    public DbSet<Domain.Entities.Payroll.Transactions.ElectronicPayrollDocument> ElectronicPayrollDocuments => Set<Domain.Entities.Payroll.Transactions.ElectronicPayrollDocument>();
    public DbSet<Domain.Entities.Payroll.Transactions.ElectronicPayrollTransmission> ElectronicPayrollTransmissions => Set<Domain.Entities.Payroll.Transactions.ElectronicPayrollTransmission>();

    // === Inventory (1: sólo vendedores; el modelo heredado se retiró en RetiroDelInventarioHeredado) ===
    public DbSet<Salesperson> Salespeople => Set<Salesperson>();
    // Feature 012 (T17, T136): documento generico de inventario, sus satelites, tipos y consecutivos.
    public DbSet<InventoryDocument> InventoryDocuments => Set<InventoryDocument>();
    public DbSet<InventoryDocumentLine> InventoryDocumentLines => Set<InventoryDocumentLine>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<DocumentLineLink> DocumentLineLinks => Set<DocumentLineLink>();
    public DbSet<DocumentPartySnapshot> DocumentPartySnapshots => Set<DocumentPartySnapshot>();
    public DbSet<DocumentTaxLine> DocumentTaxLines => Set<DocumentTaxLine>();
    public DbSet<InventoryDocumentType> InventoryDocumentTypes => Set<InventoryDocumentType>();
    // Feature 012 (T161): catalogo tributario de Core.
    public DbSet<Domain.Entities.Core.Taxes.TaxDefinition> TaxDefinitions => Set<Domain.Entities.Core.Taxes.TaxDefinition>();
    public DbSet<Domain.Entities.Core.Taxes.TaxRate> TaxRates => Set<Domain.Entities.Core.Taxes.TaxRate>();
    public DbSet<Domain.Entities.Core.Taxes.WithholdingConcept> WithholdingConcepts => Set<Domain.Entities.Core.Taxes.WithholdingConcept>();
    public DbSet<DocumentTypeWarehouse> DocumentTypeWarehouses => Set<DocumentTypeWarehouse>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    // Feature 012 (T209, US1): catalogo y bodegas (tablas en InventarioComercialNucleo, T440).
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.UnitOfMeasure> UnitsOfMeasure => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.UnitOfMeasure>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductCategory> ProductCategories => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductCategory>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.Brand> Brands => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.Brand>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.AccountingGroup> AccountingGroups => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.AccountingGroup>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.SalesChannel> SalesChannels => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.SalesChannel>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.Product> Products => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.Product>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductUnit> ProductUnits => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductUnit>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductBarcode> ProductBarcodes => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductBarcode>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductTax> ProductTaxes => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductTax>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductAccountingGroupChange> ProductAccountingGroupChanges => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductAccountingGroupChange>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.WarehouseType> WarehouseTypes => Set<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.WarehouseType>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.Warehouse> Warehouses => Set<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.Warehouse>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.WarehouseLocation> WarehouseLocations => Set<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.WarehouseLocation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.ReorderPolicy> ReorderPolicies => Set<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.ReorderPolicy>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Documents.AdjustmentCause> AdjustmentCauses => Set<IngenIA365ERP.Domain.Entities.Inventory.Documents.AdjustmentCause>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Security.UserWarehouseScope> UserWarehouseScopes => Set<IngenIA365ERP.Domain.Entities.Inventory.Security.UserWarehouseScope>();
    // Feature 012 (T250, US2): kardex y proyecciones.
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Transactions.KardexEntry> KardexEntries => Set<IngenIA365ERP.Domain.Entities.Inventory.Transactions.KardexEntry>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Projections.StockBalance> StockBalances => Set<IngenIA365ERP.Domain.Entities.Inventory.Projections.StockBalance>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Projections.StockDetail> StockDetails => Set<IngenIA365ERP.Domain.Entities.Inventory.Projections.StockDetail>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Projections.CostState> CostStates => Set<IngenIA365ERP.Domain.Entities.Inventory.Projections.CostState>();
    // Feature 012 (T284, US3): puesta en marcha, períodos y valorizado fijado al cerrar.
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Periods.InventorySetup> InventorySetups => Set<IngenIA365ERP.Domain.Entities.Inventory.Periods.InventorySetup>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Periods.InventoryPeriod> InventoryPeriods => Set<IngenIA365ERP.Domain.Entities.Inventory.Periods.InventoryPeriod>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Periods.PeriodClosingBalance> PeriodClosingBalances => Set<IngenIA365ERP.Domain.Entities.Inventory.Periods.PeriodClosingBalance>();
    // Feature 012 (T307, US4): activación de bodegas y cifras de SOLIDO.
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.GoLive.WarehouseActivation> WarehouseActivations => Set<IngenIA365ERP.Domain.Entities.Inventory.GoLive.WarehouseActivation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.GoLive.LegacyFigure> LegacyFigures => Set<IngenIA365ERP.Domain.Entities.Inventory.GoLive.LegacyFigure>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.SupplierInvoiceDetail> SupplierInvoiceDetails => Set<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.SupplierInvoiceDetail>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.SupplierInvoiceEvent> SupplierInvoiceEvents => Set<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.SupplierInvoiceEvent>();

    // === CDT (7) ===
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<CertificateEntry> CertificateEntries => Set<CertificateEntry>();
    public DbSet<CdtParameter> CdtParameters => Set<CdtParameter>();
    public DbSet<CdtRateByTerm> CdtRatesByTerm => Set<CdtRateByTerm>();
    public DbSet<CdtAssociateReference> CdtAssociateReferences => Set<CdtAssociateReference>();
    public DbSet<CdtAudit> CdtAudits => Set<CdtAudit>();
    public DbSet<CdtParameterAudit> CdtParameterAudits => Set<CdtParameterAudit>();

    // === Debit (7) ===
    public DbSet<DebitCard> DebitCards => Set<DebitCard>();
    public DbSet<DebitTransaction> DebitTransactions => Set<DebitTransaction>();
    public DbSet<DebitAgreementParameter> DebitAgreementParameters => Set<DebitAgreementParameter>();
    public DbSet<PosTerminal> PosTerminals => Set<PosTerminal>();
    public DbSet<DebitDailyParameter> DebitDailyParameters => Set<DebitDailyParameter>();
    public DbSet<DebitAgreement> DebitAgreements => Set<DebitAgreement>();
    public DbSet<DebitAgreementMember> DebitAgreementMembers => Set<DebitAgreementMember>();

    // === Treasury (3) ===
    public DbSet<Check> Checks => Set<Check>();
    public DbSet<TreasuryConcept> TreasuryConcepts => Set<TreasuryConcept>();
    public DbSet<TreasuryInvoice> TreasuryInvoices => Set<TreasuryInvoice>();

    // === Security (11) ===
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    // Feature 012 (T13, T054): claves de idempotencia (adelanto de T096).
    public DbSet<OperationKey> OperationKeys => Set<OperationKey>();
    // Feature 012 (T10, T047; T096): arrendamientos de los trabajos de fondo por cooperativa.
    public DbSet<BackgroundLease> BackgroundLeases => Set<BackgroundLease>();
    // Feature 012 (T37, T38; T061): auditoria garantizada y sello de integridad (adelanto de T096).
    public DbSet<AuditOutboxEntry> AuditOutbox => Set<AuditOutboxEntry>();
    public DbSet<AuditChainHead> AuditChainHeads => Set<AuditChainHead>();
    public DbSet<AuditAnchor> AuditAnchors => Set<AuditAnchor>();
    // Feature 012 (T21, T069): parametros con vigencia (adelanto de T096).
    public DbSet<ParameterVersion> ParameterVersions => Set<ParameterVersion>();
    // Feature 012 (T7, T9; T073-T078): bandeja de salida de mensajes de integracion (adelanto de T096).
    public DbSet<IntegrationMessage> IntegrationMessages => Set<IntegrationMessage>();
    public DbSet<IntegrationMessageDependency> IntegrationMessageDependencies => Set<IntegrationMessageDependency>();
    public DbSet<IntegrationMessageDelivery> IntegrationMessageDeliveries => Set<IntegrationMessageDelivery>();
    // Feature 012 (T33, T34; T081): aprobaciones de plataforma y montos maximos por permiso (adelanto de T096).
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalPolicyLevel> ApprovalPolicyLevels => Set<ApprovalPolicyLevel>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<PermissionAmountLimit> PermissionAmountLimits => Set<PermissionAmountLimit>();
    // Feature 012 (T39; T091-T094): alertas de plataforma. (Adelanto de T096.)
    public DbSet<AlertType> AlertTypes => Set<AlertType>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<UserMenuAccess> UserMenuAccesses => Set<UserMenuAccess>();
    public DbSet<SecurityModule> SecurityModules => Set<SecurityModule>();
    public DbSet<UserAssignment> UserAssignments => Set<UserAssignment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<PasswordPolicy> PasswordPolicies => Set<PasswordPolicy>();
    public DbSet<PasswordHistory> PasswordHistory => Set<PasswordHistory>();
    public DbSet<MfaBackupCode> MfaBackupCodes => Set<MfaBackupCode>();
    public DbSet<MfaResetRequest> MfaResetRequests => Set<MfaResetRequest>();

    // === Audit (14) ===
    public DbSet<CompanyChange> CompanyChanges => Set<CompanyChange>();
    public DbSet<UserChange> UserChanges => Set<UserChange>();
    public DbSet<VoucherTypeChange> VoucherTypeChanges => Set<VoucherTypeChange>();
    public DbSet<MasterChange> MasterChanges => Set<MasterChange>();
    public DbSet<MenuChange> MenuChanges => Set<MenuChange>();
    public DbSet<PeriodChange> PeriodChanges => Set<PeriodChange>();
    public DbSet<AssignmentChange> AssignmentChanges => Set<AssignmentChange>();
    public DbSet<AccountChange> AccountChanges => Set<AccountChange>();
    public DbSet<JournalChange> JournalChanges => Set<JournalChange>();
    public DbSet<PortfolioTransactionChange> PortfolioTransactionChanges => Set<PortfolioTransactionChange>();
    public DbSet<PortfolioMasterChange> PortfolioMasterChanges => Set<PortfolioMasterChange>();
    public DbSet<DefaultChange> DefaultChanges => Set<DefaultChange>();
    public DbSet<SavingsChange> SavingsChanges => Set<SavingsChange>();
    public DbSet<AuditReference> AuditReferences => Set<AuditReference>();

    // === Web (6) ===
    public DbSet<WebLoanApplication> WebLoanApplications => Set<WebLoanApplication>();
    public DbSet<WebAuxiliaryApplication> WebAuxiliaryApplications => Set<WebAuxiliaryApplication>();
    public DbSet<WebAffiliationApplication> WebAffiliationApplications => Set<WebAffiliationApplication>();
    public DbSet<WebService> WebServices => Set<WebService>();
    public DbSet<WebExtraPayment> WebExtraPayments => Set<WebExtraPayment>();
    public DbSet<WebDataUpdate> WebDataUpdates => Set<WebDataUpdate>();

    // === Admin (3+1) ===
    // Las tablas ADM_* NO viven aqui. El registro de cooperativas, la identidad
    // central, las invitaciones y las membresias son el plano de control del SaaS:
    // tienen UNA respuesta para toda la plataforma y viven en la base
    // administrativa, servidas por AdminDbContext.
    //
    // Estaban declaradas aqui, y ademas el barrido de configuraciones de abajo
    // arrastraba la carpeta Admin entera. Resultado medido: DOCE tablas ADM_*
    // replicadas dentro del espacio de cada cooperativa —ADM_CentralUsers incluida,
    // que guarda los hashes de contrasena de todos los logins de la plataforma— y
    // ocho claves foraneas apuntando a esas copias locales en vez de al registro
    // real.
    //
    // No era ruido inofensivo: FK_SEC_Roles_ADM_Tenants_TenantId resolvia contra la
    // copia local, vacia, asi que insertar un rol de cooperativa violaba la
    // restriccion siempre. Ninguna cooperativa llego a tener roles.
    //
    // Con una base por cooperativa deja de poder disimularse: la copia y el original
    // ya no son la misma tabla, y quien escriba en la copia lo hara donde nadie lee.

    // === Security extras (cross-tenant operadores) ===
    public DbSet<UserTenantAssignment> UserTenantAssignments => Set<UserTenantAssignment>();
    public DbSet<UserBranchAssignment> UserBranchAssignments => Set<UserBranchAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Esquema constante, y eso es el cambio.
        //
        // Con una base por cooperativa cada una tiene su propio dbo, asi que el
        // esquema deja de discriminar nada. Antes salia de
        // `_tenantInfo?.Schema ?? "dbo"`, y ese operador `??` era el aislamiento
        // entero: cuando ErpTenantInfo no llegaba —que era siempre, porque nadie lo
        // registraba— degradaba a dbo en silencio y todas las cooperativas
        // compartian espacio.
        //
        // Ahora lo que varia es la CONEXION, no el modelo. Efecto util de paso: el
        // modelo es identico para todas, asi que EF construye sus 272 entidades una
        // sola vez por proceso en lugar de una por cooperativa. Medido.
        modelBuilder.HasDefaultSchema("dbo");

        // Se excluye la carpeta Configurations/Admin: sus entidades pertenecen a
        // AdminDbContext, que las aplica una a una y de forma explicita.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly,
            t => t.Namespace?.Contains(".Configurations.Admin", StringComparison.Ordinal) != true);

        // Convenciones transversales (T011 RowVersion + T022 filtro soft-delete).
        // Se aplica después de las configuraciones específicas para que cualquier
        // override por entidad ya esté registrado. El provider activo decide el
        // mapeo de concurrencia (feature 004: ROWVERSION vs xmin).
        modelBuilder.ApplyBaseEntityConventions(Database.ProviderName);

        // Feature 012 (T206, T43): el indice de la busqueda de productos depende del motor.
        IngenIA365ERP.Persistence.Configurations.Inventory.Catalog.IndiceDeBusquedaDeProductos.Aplicar(modelBuilder, Database.ProviderName);

        // Feature 012: el documento genérico espera su par InventarioComercialNucleo (T440), que borra esta línea.
        IngenIA365ERP.Persistence.Configurations.Inventory.NucleoComercialSinMigracion.ExcluirDeLasMigraciones(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Feature 004 (D-06): precision explicita uniforme entre motores.
        // Sin esto, PostgreSQL usaria numeric ilimitado donde SQL Server usaba
        // decimal(18,2) por default. Los HasPrecision/HasColumnType por entidad
        // siguen ganando a esta convencion.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        base.ConfigureConventions(configurationBuilder);
    }

    /// <inheritdoc />
    public void DescartarCambios() => ChangeTracker.Clear();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Feature 012 (T137): hechos inmutables y documentos confirmados, antes de tocar nada (T17, T18).
        await GuardaDeInmutabilidad.VerificarAsync(this, cancellationToken);

        var now = DateTime.UtcNow;
        var userName = _currentUserService?.UserName ?? "SYSTEM";

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is AuditableEntity auditable)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedBy ??= userName;
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy ??= userName;
                }
                else if (entry.Entity is AuditableEntityLong auditableLong)
                {
                    auditableLong.CreatedAt = now;
                    auditableLong.CreatedBy ??= userName;
                    auditableLong.UpdatedAt = now;
                    auditableLong.UpdatedBy ??= userName;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is AuditableEntity auditable)
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userName;
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                }
                else if (entry.Entity is AuditableEntityLong auditableLong)
                {
                    auditableLong.UpdatedAt = now;
                    auditableLong.UpdatedBy = userName;
                    entry.Property(nameof(AuditableEntityLong.CreatedAt)).IsModified = false;
                    entry.Property(nameof(AuditableEntityLong.CreatedBy)).IsModified = false;
                }
            }
        }

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // T021: traducir conflicto de RowVersion al tipo del dominio para
            // que los handlers MediatR puedan mapearlo a Result.Failure
            // ("Concurrency.StaleRowVersion", …) sin acoplarse a EF Core.
            var entry = ex.Entries.FirstOrDefault();
            var entityType = entry?.Entity.GetType().Name ?? "Entidad";
            string? publicId = null;
            string? lastEditor = null;
            DateTime? lastEditedAt = null;

            if (entry is not null)
            {
                if (entry.Entity is BaseEntity be) publicId = be.PublicId.ToString();
                else if (entry.Entity is BaseEntityLong bel) publicId = bel.PublicId.ToString();

                if (entry.Entity is AuditableEntity ae)
                {
                    lastEditor = ae.UpdatedBy ?? ae.CreatedBy;
                    lastEditedAt = ae.UpdatedAt ?? ae.CreatedAt;
                }
                else if (entry.Entity is AuditableEntityLong ael)
                {
                    lastEditor = ael.UpdatedBy ?? ael.CreatedBy;
                    lastEditedAt = ael.UpdatedAt ?? ael.CreatedAt;
                }
            }

            throw new ConcurrencyConflictException(entityType, publicId, lastEditor, lastEditedAt, ex);
        }
    }
}
