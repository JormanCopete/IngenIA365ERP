using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Entities.CDT;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Debit;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Lending;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury;
using IngenIA365ERP.Domain.Entities.Web;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Core
    DbSet<Person> People { get; }
    DbSet<Associate> Associates { get; }
    DbSet<Branch> Branches { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<City> Cities { get; }
    DbSet<Bank> Banks { get; }
    DbSet<Company> Companies { get; }
    DbSet<Committee> Committees { get; }
    DbSet<Beneficiary> Beneficiaries { get; }
    DbSet<Reference> References { get; }
    DbSet<Country> Countries { get; }
    DbSet<Department> Departments { get; }
    DbSet<Section> Sections { get; }
    DbSet<Profession> Professions { get; }
    DbSet<Position> Positions { get; }
    DbSet<Relationship> Relationships { get; }
    DbSet<WithdrawalReason> WithdrawalReasons { get; }
    DbSet<Sport> Sports { get; }
    DbSet<CulturalActivity> CulturalActivities { get; }
    DbSet<Disease> Diseases { get; }
    DbSet<ExternalEntity> ExternalEntities { get; }
    DbSet<Agreement> Agreements { get; }
    DbSet<Course> Courses { get; }
    DbSet<Spouse> Spouses { get; }
    DbSet<PersonFinancial> PeopleFinancial { get; }
    DbSet<AssociateCategory> AssociateCategories { get; }
    DbSet<CommitteeMember> CommitteeMembers { get; }
    DbSet<EmployerCompany> EmployerCompanies { get; }
    DbSet<Advisor> Advisors { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationDeliveryFailure> NotificationDeliveryFailures { get; }

    // Compliance — Habeas data (US7)
    DbSet<HabeasDataPolicyVersion> HabeasDataPolicyVersions { get; }
    DbSet<HabeasDataConsent> HabeasDataConsents { get; }

    // Accounting (feature 009): modelo nuevo; las 33 tablas heredadas se retiraron.
    // Documentos y lineas viven en Entities.Accounting.Transactions (Principio XI).
    DbSet<AccountingSetup> AccountingSetups { get; }
    DbSet<AccountCatalog> AccountCatalogs { get; }
    DbSet<AccountCatalogEntry> AccountCatalogEntries { get; }
    DbSet<FinancialStatementItem> FinancialStatementItems { get; }
    DbSet<ChartOfAccount> ChartOfAccounts { get; }
    DbSet<AccountTaxRate> AccountTaxRates { get; }
    DbSet<VoucherType> VoucherTypes { get; }
    DbSet<CrossDocumentType> CrossDocumentTypes { get; }
    DbSet<FiscalYear> FiscalYears { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<AccountingDocument> AccountingDocuments { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<BankStatementColumnMap> BankStatementColumnMaps { get; }
    DbSet<BankReconciliation> BankReconciliations { get; }
    DbSet<BankStatementLine> BankStatementLines { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<BudgetLine> BudgetLines { get; }
    DbSet<WithholdingCertificate> WithholdingCertificates { get; }
    DbSet<WithholdingCertificateLine> WithholdingCertificateLines { get; }
    DbSet<TaxForm> TaxForms { get; }
    DbSet<TaxFormLine> TaxFormLines { get; }
    DbSet<ExogenousFormat> ExogenousFormats { get; }
    DbSet<ExogenousConcept> ExogenousConcepts { get; }
    DbSet<ExogenousConceptAccount> ExogenousConceptAccounts { get; }
    DbSet<ExogenousRun> ExogenousRuns { get; }
    DbSet<ExogenousRunLine> ExogenousRunLines { get; }
    DbSet<FixedAsset> FixedAssets { get; }
    DbSet<FixedAssetInstallment> FixedAssetInstallments { get; }
    DbSet<AssetRun> AssetRuns { get; }

    // Lending
    DbSet<LoanPortfolio> LoanPortfolios { get; }
    DbSet<LendingTransaction> LendingTransactions { get; }
    DbSet<PendingInstallment> PendingInstallments { get; }
    DbSet<CreditLineParameter> CreditLineParameters { get; }
    DbSet<TransactionCode> TransactionCodes { get; }
    DbSet<SavingsParameter> SavingsParameters { get; }
    DbSet<InterestRate> InterestRates { get; }
    DbSet<ProvisionParameter> ProvisionParameters { get; }
    DbSet<Zone> Zones { get; }
    DbSet<SubZone> SubZones { get; }
    DbSet<ZoneType> ZoneTypes { get; }
    DbSet<ApplicationStatus> ApplicationStatuses { get; }
    DbSet<ScoringParameter> ScoringParameters { get; }
    DbSet<ScoringRange> ScoringRanges { get; }
    DbSet<PayrollDeductionConcept> PayrollDeductionConcepts { get; }
    DbSet<LoanApplication> LoanApplications { get; }
    DbSet<DefaultRecord> DefaultRecords { get; }
    DbSet<CollectionCase> CollectionCases { get; }
    DbSet<Guarantee> Guarantees { get; }
    DbSet<ExtraPayment> ExtraPayments { get; }
    DbSet<SavingsAccount> SavingsAccounts { get; }
    DbSet<DepositEntry> DepositEntries { get; }
    DbSet<AccrualEntry> AccrualEntries { get; }
    DbSet<PortfolioClassification> PortfolioClassifications { get; }
    DbSet<RiskAssessment> RiskAssessments { get; }
    DbSet<PayrollDeductionEntry> PayrollDeductionEntries { get; }
    DbSet<WithdrawalStatus> WithdrawalStatuses { get; }
    DbSet<HousingParameter> HousingParameters { get; }
    DbSet<SiplaParameter> SiplaParameters { get; }
    DbSet<PeriodicityParameter> PeriodicityParameters { get; }
    DbSet<TermRate> TermRates { get; }
    DbSet<PortfolioAccount> PortfolioAccounts { get; }
    DbSet<ContributionReduction> ContributionReductions { get; }
    DbSet<AssociateWithdrawal> AssociateWithdrawals { get; }
    DbSet<CertificateEntry> CertificateEntries { get; }

    // Payroll
    DbSet<Employee> Employees { get; }
    DbSet<PayrollConcept> PayrollConcepts { get; }
    DbSet<PayPeriod> PayPeriods { get; }
    DbSet<HealthInsuranceProvider> HealthInsuranceProviders { get; }
    DbSet<WorkRiskProvider> WorkRiskProviders { get; }
    DbSet<WorkRiskRate> WorkRiskRates { get; }
    DbSet<PensionProvider> PensionProviders { get; }
    DbSet<SeveranceProvider> SeveranceProviders { get; }
    DbSet<FamilyCompensationFund> FamilyCompensationFunds { get; }
    DbSet<ConceptAccount> ConceptAccounts { get; }
    DbSet<WithholdingParameter> WithholdingParameters { get; }
    DbSet<WithholdingCause> WithholdingCauses { get; }
    DbSet<AutoContributionParam> AutoContributionParams { get; }
    DbSet<PayrollTransaction> PayrollTransactions { get; }
    DbSet<PayrollEntry> PayrollEntries { get; }
    DbSet<SalaryChange> SalaryChanges { get; }
    DbSet<Absence> Absences { get; }
    DbSet<TaxCertificate> TaxCertificates { get; }

    // Payroll — feature 005 (novedades y liquidacion)
    DbSet<PayrollPlan> PayrollPlans { get; }
    DbSet<PayrollConceptDefinition> PayrollConceptDefinitions { get; }
    DbSet<PayrollConceptDefinitionAccount> PayrollConceptDefinitionAccounts { get; }
    DbSet<PayrollLegalParameter> PayrollLegalParameters { get; }
    DbSet<PayrollLegalParameterRange> PayrollLegalParameterRanges { get; }
    DbSet<PayrollNovelty> PayrollNovelties { get; }
    DbSet<PayrollRecurringNovelty> PayrollRecurringNovelties { get; }
    DbSet<EmployeeWithholdingRate> EmployeeWithholdingRates { get; }
    DbSet<EmployeeTaxDeduction> EmployeeTaxDeductions { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PayrollRun> PayrollRuns { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PayrollRunEmployee> PayrollRunEmployees { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PayrollRunLine> PayrollRunLines { get; }
    DbSet<PayrollPayment> PayrollPayments { get; }
    DbSet<PayslipDelivery> PayslipDeliveries { get; }

    // Payroll — feature 010 (prestaciones, retiro, procedimiento 2; entrega N1)
    DbSet<CompanyPolicy> CompanyPolicies { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<EmployeeBenefitOpeningBalance> EmployeeBenefitOpeningBalances { get; }
    DbSet<VacationMovement> VacationMovements { get; }
    DbSet<TerminationReason> TerminationReasons { get; }
    DbSet<EmploymentTermination> EmploymentTerminations { get; }
    DbSet<SettlementDeduction> SettlementDeductions { get; }
    DbSet<WithholdingRateCalculation> WithholdingRateCalculations { get; }
    DbSet<WithholdingRateCalculationMonth> WithholdingRateCalculationMonths { get; }
    DbSet<SeveranceFundDeposit> SeveranceFundDeposits { get; }

    // Inventory
    DbSet<Product> Products { get; }
    DbSet<ProductGroup> ProductGroups { get; }
    DbSet<PrimaryGroup> PrimaryGroups { get; }
    DbSet<SecondaryGroup> SecondaryGroups { get; }
    DbSet<InventoryTransactionType> InventoryTransactionTypes { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Location> Locations { get; }
    DbSet<SalesPoint> SalesPoints { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<Salesperson> Salespeople { get; }
    DbSet<DiscountType> DiscountTypes { get; }
    DbSet<PriceListType> PriceListTypes { get; }
    DbSet<ProductAccount> ProductAccounts { get; }
    DbSet<VatAccount> VatAccounts { get; }
    DbSet<CommissionParameter> CommissionParameters { get; }
    DbSet<InventoryDocument> InventoryDocuments { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }
    DbSet<InventoryInvoice> InventoryInvoices { get; }
    DbSet<PhysicalInventory> PhysicalInventories { get; }

    // CDT
    DbSet<Certificate> Certificates { get; }
    DbSet<CdtParameter> CdtParameters { get; }
    DbSet<CdtRateByTerm> CdtRatesByTerm { get; }

    // Debit
    DbSet<DebitCard> DebitCards { get; }
    DbSet<DebitAgreementParameter> DebitAgreementParameters { get; }
    DbSet<PosTerminal> PosTerminals { get; }
    DbSet<DebitDailyParameter> DebitDailyParameters { get; }
    DbSet<DebitTransaction> DebitTransactions { get; }
    DbSet<DebitAgreement> DebitAgreements { get; }

    // Treasury
    DbSet<Check> Checks { get; }
    DbSet<TreasuryConcept> TreasuryConcepts { get; }
    DbSet<TreasuryInvoice> TreasuryInvoices { get; }

    // Security
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }

    // Parametros de configuracion de la cooperativa (COR_SystemSettings).
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<LoginAttempt> LoginAttempts { get; }
    DbSet<UserTenantAssignment> UserTenantAssignments { get; }
    DbSet<UserBranchAssignment> UserBranchAssignments { get; }
    DbSet<PasswordPolicy> PasswordPolicies { get; }
    DbSet<PasswordHistory> PasswordHistory { get; }
    DbSet<MfaBackupCode> MfaBackupCodes { get; }
    DbSet<MfaResetRequest> MfaResetRequests { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }

    // Admin
    // Tenants y TenantBranches se retiraron: son del plano de control del SaaS y
    // viven en IAdminDbContext. Quien las necesite desde un handler tiene que pedir
    // ese contexto y decirlo, en vez de alcanzarlas por la puerta de atras.

    /// <summary>
    /// Descarta todo lo que el contexto tenga sin guardar (feature 009, R3): lo usa el reintento
    /// por concurrencia entre un intento y el siguiente. No toca la base.
    /// </summary>
    void DescartarCambios();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
