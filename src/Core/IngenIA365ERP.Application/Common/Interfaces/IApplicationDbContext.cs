using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Entities.CDT;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Debit;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Lending;
using IngenIA365ERP.Domain.Entities.Parameters;
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
    DbSet<BankFileFormat> BankFileFormats { get; }
    DbSet<BankFileFormatField> BankFileFormatFields { get; }
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
    DbSet<Domain.Entities.Payroll.Transactions.BankDisbursementFile> BankDisbursementFiles { get; }
    DbSet<Domain.Entities.Payroll.Transactions.BankDisbursementFileLine> BankDisbursementFileLines { get; }
    DbSet<Domain.Entities.Payroll.Pila.PilaSettings> PilaSettings { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PilaGeneration> PilaGenerations { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PilaGenerationLine> PilaGenerationLines { get; }
    DbSet<Domain.Entities.Payroll.Pila.PilaIssue> PilaIssues { get; }
    DbSet<Domain.Entities.Payroll.ElectronicPayroll.ElectronicPayrollSettings> ElectronicPayrollSettings { get; }
    DbSet<Domain.Entities.Payroll.ElectronicPayroll.ElectronicPayrollNumberingRange> ElectronicPayrollNumberingRanges { get; }
    DbSet<Domain.Entities.Payroll.Transactions.ElectronicPayrollDocument> ElectronicPayrollDocuments { get; }
    DbSet<Domain.Entities.Payroll.Transactions.ElectronicPayrollTransmission> ElectronicPayrollTransmissions { get; }

    // Inventory
    DbSet<Salesperson> Salespeople { get; }
    // Feature 012 (T17, T136): documento generico de inventario. Number y NextValue los escribe solo Numerador;
    // DocumentPartySnapshots y DocumentTaxLines son hechos (solo insercion).
    DbSet<InventoryDocument> InventoryDocuments { get; }
    DbSet<InventoryDocumentLine> InventoryDocumentLines { get; }
    DbSet<DocumentLink> DocumentLinks { get; }
    DbSet<DocumentLineLink> DocumentLineLinks { get; }
    DbSet<DocumentPartySnapshot> DocumentPartySnapshots { get; }
    DbSet<DocumentTaxLine> DocumentTaxLines { get; }
    DbSet<InventoryDocumentType> InventoryDocumentTypes { get; }
    DbSet<DocumentTypeWarehouse> DocumentTypeWarehouses { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    // Feature 012 (T209, US1): catalogo y bodegas. ProductAccountingGroupChanges es un hecho (solo insercion; lo escribe
    // ChangeProductAccountingGroupCommand, US3). INV_UserWarehouseScopes se lee y escribe solo por IAsignacionesDeBodega.
    DbSet<Domain.Entities.Inventory.Catalog.UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductCategory> ProductCategories { get; }
    DbSet<Domain.Entities.Inventory.Catalog.Brand> Brands { get; }
    DbSet<Domain.Entities.Inventory.Catalog.AccountingGroup> AccountingGroups { get; }
    DbSet<Domain.Entities.Inventory.Catalog.SalesChannel> SalesChannels { get; }
    DbSet<Domain.Entities.Inventory.Catalog.Product> Products { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductUnit> ProductUnits { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductBarcode> ProductBarcodes { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductTax> ProductTaxes { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductAccountingGroupChange> ProductAccountingGroupChanges { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.WarehouseType> WarehouseTypes { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.Warehouse> Warehouses { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.WarehouseLocation> WarehouseLocations { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.ReorderPolicy> ReorderPolicies { get; }
    DbSet<Domain.Entities.Inventory.Documents.AdjustmentCause> AdjustmentCauses { get; }
    DbSet<Domain.Entities.Inventory.Security.UserWarehouseScope> UserWarehouseScopes { get; }
    // Feature 012 (T250, US2): el kardex y sus proyecciones. Los escriben solo RegistroDeKardex y
    // RebuildInventoryProjectionsCommand (NadieEscribeElKardexFueraDelRegistro).
    DbSet<Domain.Entities.Inventory.Transactions.KardexEntry> KardexEntries { get; }
    DbSet<Domain.Entities.Inventory.Projections.StockBalance> StockBalances { get; }
    DbSet<Domain.Entities.Inventory.Projections.StockDetail> StockDetails { get; }
    DbSet<Domain.Entities.Inventory.Projections.CostState> CostStates { get; }

    // Feature 012 (T22, T161): catalogo tributario de Core. Lo escriben solo los comandos de Core/Taxes (y su
    // plantilla y semilla); lo lee para el motor solo LectorDeCatalogoTributario.
    DbSet<Domain.Entities.Core.Taxes.TaxDefinition> TaxDefinitions { get; }
    DbSet<Domain.Entities.Core.Taxes.TaxRate> TaxRates { get; }
    DbSet<Domain.Entities.Core.Taxes.WithholdingConcept> WithholdingConcepts { get; }

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

    // Feature 012 (T13, T054): claves de idempotencia de las operaciones de pantalla. Las lee y escribe
    // solo IdempotencyBehavior. (Adelanto de T096, que registra el resto de DbSet de la plataforma.)
    DbSet<OperationKey> OperationKeys { get; }

    // Feature 012 (T10, T047; T096): arrendamientos de los trabajos de fondo, uno por nombre y por cooperativa. Los
    // toma, renueva y suelta solo ArrendamientosEnBase (IArrendamientos); las filas las siembra la migracion.
    DbSet<BackgroundLease> BackgroundLeases { get; }

    // Feature 012 (T37, T38; T061–T066): auditoria garantizada de los modulos encadenados. Escriben
    // AuditableEntityInterceptor, AuditBehavior y AuditoriaEncadenada; sella y reenvia solo el
    // AuditOutboxForwarder. (Adelanto de T096.)
    DbSet<AuditOutboxEntry> AuditOutbox { get; }
    DbSet<AuditChainHead> AuditChainHeads { get; }
    DbSet<AuditAnchor> AuditAnchors { get; }

    // Feature 012 (T21, T069-T071): parametros con vigencia. Los leen solo LectorDeParametros y los escribe solo
    // AddParameterVersionCommandHandler (LosParametrosSeLeenEnUnSoloSitio). (Adelanto de T096.)
    DbSet<ParameterVersion> ParameterVersions { get; }

    // Feature 012 (T7, T9; T073-T078): bandeja de salida de mensajes de integracion. Escribe mensajes, entregas y
    // dependencias solo EmisorDeMensajes (dentro del SaveChanges del documento); las entregas las actualizan
    // despues los comandos de I2. (Adelanto de T096.)
    DbSet<IntegrationMessage> IntegrationMessages { get; }
    DbSet<IntegrationMessageDependency> IntegrationMessageDependencies { get; }
    DbSet<IntegrationMessageDelivery> IntegrationMessageDeliveries { get; }

    // Feature 012 (T33, T34; T081-T085): aprobaciones de plataforma y montos maximos por permiso. Escriben solo
    // MotorDeAprobaciones (solicitudes y decisiones), SaveApprovalPolicyCommand y SetPermissionAmountLimitCommand;
    // lee los limites ILimitesPorPermiso. (Adelanto de T096.)
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<ApprovalPolicyLevel> ApprovalPolicyLevels { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalDecision> ApprovalDecisions { get; }
    DbSet<PermissionAmountLimit> PermissionAmountLimits { get; }

    // Feature 012 (T39; T091-T094): alertas de plataforma. Escriben solo IAlertas (levantar, atender por proceso),
    // AttendAlertCommand, SaveAlertTypeCommand y AlertTypesSeeder. (Adelanto de T096.)
    DbSet<AlertType> AlertTypes { get; }
    DbSet<Alert> Alerts { get; }

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
