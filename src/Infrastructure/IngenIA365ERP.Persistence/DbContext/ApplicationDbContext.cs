using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
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
    public DbSet<Domain.Entities.Payroll.Transactions.PayrollLoanDeduction> PayrollLoanDeductions => Set<Domain.Entities.Payroll.Transactions.PayrollLoanDeduction>();
    public DbSet<Domain.Entities.Payroll.Transactions.PayrollAccountDeduction> PayrollAccountDeductions => Set<Domain.Entities.Payroll.Transactions.PayrollAccountDeduction>();
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
    // Feature 012 (T307, US4): activación de bodegas y cifras de referencia.
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.GoLive.WarehouseActivation> WarehouseActivations => Set<IngenIA365ERP.Domain.Entities.Inventory.GoLive.WarehouseActivation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.GoLive.LegacyFigure> LegacyFigures => Set<IngenIA365ERP.Domain.Entities.Inventory.GoLive.LegacyFigure>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.SupplierInvoiceDetail> SupplierInvoiceDetails => Set<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.SupplierInvoiceDetail>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.SupplierInvoiceEvent> SupplierInvoiceEvents => Set<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.SupplierInvoiceEvent>();
    // Feature 012 (T366, US10): faltantes y sobrantes de los traslados.
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Documents.TransferDiscrepancy> TransferDiscrepancies => Set<IngenIA365ERP.Domain.Entities.Inventory.Documents.TransferDiscrepancy>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Documents.CountSnapshotLine> CountSnapshotLines => Set<IngenIA365ERP.Domain.Entities.Inventory.Documents.CountSnapshotLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Documents.CountCapture> CountCaptures => Set<IngenIA365ERP.Domain.Entities.Inventory.Documents.CountCapture>();
    // Feature 012, I5 (T835): cruce a tres vías, reparto de costos adicionales, capas PEPS y sus consumos.
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.PurchaseMatchLine> PurchaseMatchLines => Set<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.PurchaseMatchLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.LandedCostAllocation> LandedCostAllocations => Set<IngenIA365ERP.Domain.Entities.Inventory.Purchasing.LandedCostAllocation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Projections.CostLayer> CostLayers => Set<IngenIA365ERP.Domain.Entities.Inventory.Projections.CostLayer>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Transactions.LayerConsumption> LayerConsumptions => Set<IngenIA365ERP.Domain.Entities.Inventory.Transactions.LayerConsumption>();
    // Feature 012, I6 (T859; ComercioAmpliado): catálogo avanzado, lotes y series, reservas y promociones.
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductComponent> ProductComponents => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductComponent>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.VariantAttribute> VariantAttributes => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.VariantAttribute>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.VariantAttributeValue> VariantAttributeValues => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.VariantAttributeValue>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductVariantValue> ProductVariantValues => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.ProductVariantValue>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.Lot> Lots => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.Lot>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Catalog.Serial> Serials => Set<IngenIA365ERP.Domain.Entities.Inventory.Catalog.Serial>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.Reservation> Reservations => Set<IngenIA365ERP.Domain.Entities.Inventory.Warehousing.Reservation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pricing.Promotion> Promotions => Set<IngenIA365ERP.Domain.Entities.Inventory.Pricing.Promotion>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pricing.PromotionScope> PromotionScopes => Set<IngenIA365ERP.Domain.Entities.Inventory.Pricing.PromotionScope>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pricing.PromotionTier> PromotionTiers => Set<IngenIA365ERP.Domain.Entities.Inventory.Pricing.PromotionTier>();
    // Feature 012 (T585, I3): medios de pago de Core, punto de venta y caja, precios y satélites de la venta (VentasYPuntoDeVenta).
    public DbSet<IngenIA365ERP.Domain.Entities.Core.Payments.PaymentMeans> PaymentMeans => Set<IngenIA365ERP.Domain.Entities.Core.Payments.PaymentMeans>();
    public DbSet<IngenIA365ERP.Domain.Entities.Core.Payments.CardNetwork> CardNetworks => Set<IngenIA365ERP.Domain.Entities.Core.Payments.CardNetwork>();
    public DbSet<IngenIA365ERP.Domain.Entities.Core.Payments.CardAcquirer> CardAcquirers => Set<IngenIA365ERP.Domain.Entities.Core.Payments.CardAcquirer>();
    public DbSet<IngenIA365ERP.Domain.Entities.Core.Payments.CardTerminal> CardTerminals => Set<IngenIA365ERP.Domain.Entities.Core.Payments.CardTerminal>();
    public DbSet<IngenIA365ERP.Domain.Entities.Core.Payments.CashDenomination> CashDenominations => Set<IngenIA365ERP.Domain.Entities.Core.Payments.CashDenomination>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.PointOfSale> PointsOfSale => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.PointOfSale>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashRegister> CashRegisters => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashRegister>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashRegisterDocumentType> CashRegisterDocumentTypes => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashRegisterDocumentType>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.PaymentMeansPointOfSale> PaymentMeansPointsOfSale => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.PaymentMeansPointOfSale>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.PaymentMeansChannel> PaymentMeansChannels => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.PaymentMeansChannel>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.PaymentMeansDocumentType> PaymentMeansDocumentTypes => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.PaymentMeansDocumentType>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashSession> CashSessions => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashSession>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashMovementDetail> CashMovementDetails => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashMovementDetail>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCount> CashCounts => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCount>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCountLine> CashCountLines => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCountLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCountDenomination> CashCountDenominations => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCountDenomination>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCountTerminalBatch> CashCountTerminalBatches => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCountTerminalBatch>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCountReferenceCheck> CashCountReferenceChecks => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashCountReferenceCheck>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashDocumentLine> CashDocumentLines => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.CashDocumentLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.DayClose> DayCloses => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.DayClose>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pos.DayCloseLine> DayCloseLines => Set<IngenIA365ERP.Domain.Entities.Inventory.Pos.DayCloseLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Documents.DocumentPayment> DocumentPayments => Set<IngenIA365ERP.Domain.Entities.Inventory.Documents.DocumentPayment>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Documents.LendingObligationResponse> LendingObligationResponses => Set<IngenIA365ERP.Domain.Entities.Inventory.Documents.LendingObligationResponse>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Documents.VoucherRedemption> VoucherRedemptions => Set<IngenIA365ERP.Domain.Entities.Inventory.Documents.VoucherRedemption>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Documents.DocumentLineDiscount> DocumentLineDiscounts => Set<IngenIA365ERP.Domain.Entities.Inventory.Documents.DocumentLineDiscount>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pricing.PriceList> PriceLists => Set<IngenIA365ERP.Domain.Entities.Inventory.Pricing.PriceList>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pricing.PriceListItem> PriceListItems => Set<IngenIA365ERP.Domain.Entities.Inventory.Pricing.PriceListItem>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Pricing.DiscountCap> DiscountCaps => Set<IngenIA365ERP.Domain.Entities.Inventory.Pricing.DiscountCap>();
    public DbSet<IngenIA365ERP.Domain.Entities.Inventory.Security.UserPointOfSaleScope> UserPointOfSaleScopes => Set<IngenIA365ERP.Domain.Entities.Inventory.Security.UserPointOfSaleScope>();

    // === Facturación electrónica DIAN (feature 012, I4, T698) ===
    public DbSet<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.ElectronicEmissionSetting> ElectronicEmissionSettings => Set<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.ElectronicEmissionSetting>();
    public DbSet<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.DianNumberingResolution> DianNumberingResolutions => Set<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.DianNumberingResolution>();
    public DbSet<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.DianResolutionChannel> DianResolutionChannels => Set<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.DianResolutionChannel>();
    public DbSet<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.DianContingencyEvent> DianContingencyEvents => Set<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.DianContingencyEvent>();
    public DbSet<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.ElectronicDocument> ElectronicDocuments => Set<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.ElectronicDocument>();
    public DbSet<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion> ElectronicDocumentVersions => Set<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion>();
    public DbSet<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentTransmission> ElectronicDocumentTransmissions => Set<IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentTransmission>();

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
    // Feature 012, entrega I2 (T483).
    public DbSet<IntegrationDeliveryAttempt> IntegrationDeliveryAttempts => Set<IntegrationDeliveryAttempt>();
    public DbSet<IntegrationBatch> IntegrationBatches => Set<IntegrationBatch>();
    public DbSet<IntegrationBatchCounter> IntegrationBatchCounters => Set<IntegrationBatchCounter>();
    public DbSet<InventoryPostingRule> InventoryPostingRules => Set<InventoryPostingRule>();
    public DbSet<InventoryVoucherMapping> InventoryVoucherMappings => Set<InventoryVoucherMapping>();
    public DbSet<InventoryPosting> InventoryPostings => Set<InventoryPosting>();
    // Feature 013, US7 (T0636): el lado contable de la integración con Cartera.
    public DbSet<IngenIA365ERP.Domain.Entities.Accounting.Lending.LendingPostingRule> LendingPostingRules => Set<IngenIA365ERP.Domain.Entities.Accounting.Lending.LendingPostingRule>();
    public DbSet<IngenIA365ERP.Domain.Entities.Accounting.Lending.LendingVoucherMapping> LendingVoucherMappings => Set<IngenIA365ERP.Domain.Entities.Accounting.Lending.LendingVoucherMapping>();
    public DbSet<IngenIA365ERP.Domain.Entities.Accounting.Transactions.LendingPosting> LendingPostings => Set<IngenIA365ERP.Domain.Entities.Accounting.Transactions.LendingPosting>();
    public DbSet<IngenIA365ERP.Domain.Entities.Accounting.Lending.RegulatoryIndicator> RegulatoryIndicators => Set<IngenIA365ERP.Domain.Entities.Accounting.Lending.RegulatoryIndicator>();
    // Feature 012 (T33, T34; T081): aprobaciones de plataforma y montos maximos por permiso (adelanto de T096).
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalPolicyLevel> ApprovalPolicyLevels => Set<ApprovalPolicyLevel>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<PermissionAmountLimit> PermissionAmountLimits => Set<PermissionAmountLimit>();
    // Feature 012 (T39; T091-T094): alertas de plataforma. (Adelanto de T096.)
    public DbSet<AlertType> AlertTypes => Set<AlertType>();
    public DbSet<Alert> Alerts => Set<Alert>();
    // Feature 013 (T37, T39, T41): copia de los valores nacionales, su bitácora de réplicas y las revisiones.
    public DbSet<Domain.Entities.Core.NationalValues.NationalValue> NationalValues => Set<Domain.Entities.Core.NationalValues.NationalValue>();
    public DbSet<Domain.Entities.Core.NationalValues.RegulatoryDocument> RegulatoryDocuments => Set<Domain.Entities.Core.NationalValues.RegulatoryDocument>();
    public DbSet<Domain.Entities.Core.NationalValues.NationalValueReplication> NationalValueReplications => Set<Domain.Entities.Core.NationalValues.NationalValueReplication>();
    public DbSet<Domain.Entities.Core.NationalValues.NationalValueReview> NationalValueReviews => Set<Domain.Entities.Core.NationalValues.NationalValueReview>();
    public DbSet<Domain.Entities.Core.RegulatoryProfileVersion> RegulatoryProfileVersions => Set<Domain.Entities.Core.RegulatoryProfileVersion>();
    // Feature 013 (T0087, T0088): listas restrictivas y solicitudes del titular.
    public DbSet<Domain.Entities.Compliance.RestrictiveListLoad> RestrictiveListLoads => Set<Domain.Entities.Compliance.RestrictiveListLoad>();
    public DbSet<Domain.Entities.Compliance.RestrictiveListEntry> RestrictiveListEntries => Set<Domain.Entities.Compliance.RestrictiveListEntry>();
    public DbSet<Domain.Entities.Compliance.DataSubjectRequest> DataSubjectRequests => Set<Domain.Entities.Compliance.DataSubjectRequest>();
    // Feature 013 (T0077): el registro de participantes de la integración.
    public DbSet<IntegrationParticipant> IntegrationParticipants => Set<IntegrationParticipant>();
    public DbSet<Domain.Entities.Core.ExternalChannelSetting> ExternalChannelSettings => Set<Domain.Entities.Core.ExternalChannelSetting>();
    public DbSet<IntegrationParticipantAdoption> IntegrationParticipantAdoptions => Set<IntegrationParticipantAdoption>();
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

    // Feature 013, fase 3 (T0236): Cartera nueva, migración CarteraNucleo. Sólo forma; cada historia les da comportamiento.
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CreditReportClaim> CreditReportClaims => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.CreditReportClaim>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.DebtorMessage> DebtorMessages => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.DebtorMessage>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.DebtorMessageEvent> DebtorMessageEvents => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.DebtorMessageEvent>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.PriorNotice> PriorNotices => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.PriorNotice>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CommercialTermEquivalence> CommercialTermEquivalences => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.CommercialTermEquivalence>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.ContributionBalanceLoad> ContributionBalanceLoads => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.ContributionBalanceLoad>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLine> CreditLines => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineCollateralRule> CreditLineCollateralRules => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineCollateralRule>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineDocumentRequirement> CreditLineDocumentRequirements => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineDocumentRequirement>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineInsurance> CreditLineInsurances => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineInsurance>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineRateBand> CreditLineRateBands => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineRateBand>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineVersion> CreditLineVersions => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineVersion>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingFactType> LendingFactTypes => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingFactType>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingNumberSequence> LendingNumberSequences => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingNumberSequence>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingSetup> LendingSetups => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingSetup>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.UserCreditLineScope> UserCreditLineScopes => Set<IngenIA365ERP.Domain.Entities.Lending.Configuration.UserCreditLineScope>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentEvent> IssuedDocumentEvents => Set<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentEvent>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentLoan> IssuedDocumentLoans => Set<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentLoan>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedLendingDocument> IssuedLendingDocuments => Set<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedLendingDocument>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.RegulatoryFile> RegulatoryFiles => Set<IngenIA365ERP.Domain.Entities.Lending.Documents.RegulatoryFile>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.RegulatoryFileLine> RegulatoryFileLines => Set<IngenIA365ERP.Domain.Entities.Lending.Documents.RegulatoryFileLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionLevel> AttributionLevels => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionLevel>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionMatrix> AttributionMatrices => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionMatrix>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionRule> AttributionRules => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionRule>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.CaseParticipation> CaseParticipations => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.CaseParticipation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.ConnectedGroup> ConnectedGroups => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.ConnectedGroup>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.ConnectedGroupMember> ConnectedGroupMembers => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.ConnectedGroupMember>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.CreditDecision> CreditDecisions => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.CreditDecision>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBody> GoverningBodies => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBody>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBodyMember> GoverningBodyMembers => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBodyMember>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBodyRuleVersion> GoverningBodyRuleVersions => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBodyRuleVersion>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.RelatedParty> RelatedParties => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.RelatedParty>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.Session> LendingSessions => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.Session>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionAttendance> SessionAttendances => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionAttendance>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionItem> SessionItems => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionItem>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionVote> SessionVotes => Set<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionVote>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.AccountingFactPart> AccountingFactParts => Set<IngenIA365ERP.Domain.Entities.Lending.Integration.AccountingFactPart>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingInboundMessage> LendingInboundMessages => Set<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingInboundMessage>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingInboundMessageEvaluation> LendingInboundMessageEvaluations => Set<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingInboundMessageEvaluation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingProcessRun> LendingProcessRuns => Set<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingProcessRun>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingProcessRunError> LendingProcessRunErrors => Set<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingProcessRunError>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.InsolvencyProceeding> InsolvencyProceedings => Set<IngenIA365ERP.Domain.Entities.Lending.Loans.InsolvencyProceeding>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.InsuranceClaim> InsuranceClaims => Set<IngenIA365ERP.Domain.Entities.Lending.Loans.InsuranceClaim>();
    // Feature 013, US9 (T0697): riesgo y cierres (CarteraRiesgo).
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyClose> MonthlyCloses => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyClose>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseLoan> MonthlyCloseLoans => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseLoan>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseGeneralImpairment> MonthlyCloseGeneralImpairments => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseGeneralImpairment>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseReopening> MonthlyCloseReopenings => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseReopening>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.LoanArrearsHistoryEntry> LoanArrearsHistory => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.LoanArrearsHistoryEntry>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.AnalysisRating> AnalysisRatings => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.AnalysisRating>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.RiskPolicyVersion> RiskPolicyVersions => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.RiskPolicyVersion>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.ImpairmentModelElection> ImpairmentModelElections => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.ImpairmentModelElection>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.ArrearsHistoryLoad> ArrearsHistoryLoads => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.ArrearsHistoryLoad>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.Loan> Loans => Set<IngenIA365ERP.Domain.Entities.Lending.Loans.Loan>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanControl> LoanControls => Set<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanControl>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanParty> LoanParties => Set<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanParty>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanTermsVersion> LoanTermsVersions => Set<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanTermsVersion>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.LendingActivation> LendingActivations => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.LendingActivation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationArrearsHistoryRow> MigrationArrearsHistoryRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationArrearsHistoryRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationAuthorizationRow> MigrationAuthorizationRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationAuthorizationRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBalanceRow> MigrationBalanceRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBalanceRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBlockRow> MigrationBlockRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBlockRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBureauReportRow> MigrationBureauReportRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBureauReportRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationChargedOffRow> MigrationChargedOffRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationChargedOffRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationClaimRow> MigrationClaimRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationClaimRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCoDebtorRow> MigrationCoDebtorRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCoDebtorRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCollateralRow> MigrationCollateralRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCollateralRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCreditLimitRow> MigrationCreditLimitRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCreditLimitRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationInstallmentRow> MigrationInstallmentRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationInstallmentRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoad> MigrationLoads => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoad>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoadDifference> MigrationLoadDifferences => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoadDifference>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoadPart> MigrationLoadParts => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoadPart>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoanRow> MigrationLoanRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoanRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationPriorNoticeRow> MigrationPriorNoticeRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationPriorNoticeRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRelatedPartyRow> MigrationRelatedPartyRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRelatedPartyRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRestructuringRow> MigrationRestructuringRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRestructuringRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRiskDataRow> MigrationRiskDataRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRiskDataRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationSaleReconciliation> MigrationSaleReconciliations => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationSaleReconciliation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationSaleRow> MigrationSaleRows => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationSaleRow>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.ProvisionalCollection> ProvisionalCollections => Set<IngenIA365ERP.Domain.Entities.Lending.Migration.ProvisionalCollection>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementFile> DisbursementFiles => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementFile>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementFileLine> DisbursementFileLines => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementFileLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementOrder> DisbursementOrders => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementOrder>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementOrderResult> DisbursementOrderResults => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementOrderResult>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.InstallmentNonDeduction> InstallmentNonDeductions => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.InstallmentNonDeduction>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanOperation> LoanOperations => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanOperation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.BureauQuery> BureauQueries => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.BureauQuery>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.Collateral> Collaterals => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.Collateral>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplication> CreditApplications => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplication>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationCheck> CreditApplicationChecks => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationCheck>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationDocument> CreditApplicationDocuments => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationDocument>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationParty> CreditApplicationParties => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationParty>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditLimit> CreditLimits => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditLimit>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditScore> CreditScores => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditScore>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.DebtorBlock> DebtorBlocks => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.DebtorBlock>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.DebtorControl> DebtorControls => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.DebtorControl>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.LoanNumberReservation> LoanNumberReservations => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.LoanNumberReservation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.PrepaymentPreference> PrepaymentPreferences => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.PrepaymentPreference>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.PromissoryNote> PromissoryNotes => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.PromissoryNote>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.ScreeningResult> ScreeningResults => Set<IngenIA365ERP.Domain.Entities.Lending.Origination.ScreeningResult>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.LoanPayrollLink> LoanPayrollLinks => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.LoanPayrollLink>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreement> PayrollAgreements => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreement>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementVersion> PayrollAgreementVersions => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementVersion>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementContact> PayrollAgreementContacts => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementContact>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementResultCode> PayrollAgreementResultCodes => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementResultCode>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementFileGroup> PayrollAgreementFileGroups => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementFileGroup>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementConceptEquivalence> PayrollAgreementConceptEquivalences => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementConceptEquivalence>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionFile> PayrollDeductionFiles => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionFile>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionFileLine> PayrollDeductionFileLines => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionFileLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionResult> PayrollDeductionResults => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionResult>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.NonDeductionFollowUp> NonDeductionFollowUps => Set<IngenIA365ERP.Domain.Entities.Lending.Payroll.NonDeductionFollowUp>();
    // K3 (feature 013, CarteraNovedades).
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNovelty> LoanNovelties => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNovelty>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNoveltyLoan> LoanNoveltyLoans => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNoveltyLoan>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNoveltyConsent> LoanNoveltyConsents => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNoveltyConsent>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.Waiver> Waivers => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.Waiver>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.WaiverLine> WaiverLines => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.WaiverLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.WriteOffBatch> WriteOffBatches => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.WriteOffBatch>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.WriteOffBatchItem> WriteOffBatchItems => Set<IngenIA365ERP.Domain.Entities.Lending.Operations.WriteOffBatchItem>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.PortfolioEvaluation> PortfolioEvaluations => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.PortfolioEvaluation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.PortfolioEvaluationLoan> PortfolioEvaluationLoans => Set<IngenIA365ERP.Domain.Entities.Lending.Risk.PortfolioEvaluationLoan>();
    // K4 (feature 013, CarteraCobranza).
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.MessageCampaign> MessageCampaigns => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.MessageCampaign>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionStage> CollectionStages => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionStage>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionStageHistoryEntry> CollectionStageHistory => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionStageHistoryEntry>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectorAssignmentRule> CollectorAssignmentRules => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectorAssignmentRule>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectorAssignment> CollectorAssignments => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectorAssignment>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionAction> CollectionActions => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionAction>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentPromise> PaymentPromises => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentPromise>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentAgreement> PaymentAgreements => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentAgreement>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentAgreementInstallment> PaymentAgreementInstallments => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentAgreementInstallment>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.LegalProcess> LegalProcesses => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.LegalProcess>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.LegalProcessAction> LegalProcessActions => Set<IngenIA365ERP.Domain.Entities.Lending.Collections.LegalProcessAction>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Projections.LoanDailyPosition> LoanDailyPositions => Set<IngenIA365ERP.Domain.Entities.Lending.Projections.LoanDailyPosition>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Schedules.RepaymentInstallment> RepaymentInstallments => Set<IngenIA365ERP.Domain.Entities.Lending.Schedules.RepaymentInstallment>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Schedules.RepaymentScheduleVersion> RepaymentScheduleVersions => Set<IngenIA365ERP.Domain.Entities.Lending.Schedules.RepaymentScheduleVersion>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Transactions.LoanTransaction> LoanTransactions => Set<IngenIA365ERP.Domain.Entities.Lending.Transactions.LoanTransaction>();
    // Feature 014 (T021–T026): las cuentas de captación y aportes y su libro.
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccount> MemberAccounts => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccount>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccountParty> MemberAccountParties => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccountParty>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.GmfExemptAccount> GmfExemptAccounts => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.GmfExemptAccount>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountInstallment> AccountInstallments => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountInstallment>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountInstallmentNonDeduction> AccountInstallmentNonDeductions => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountInstallmentNonDeduction>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.ContributionRequest> ContributionRequests => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.ContributionRequest>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawal> MembershipWithdrawals => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawal>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawalLine> MembershipWithdrawalLines => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawalLine>();
    public DbSet<IngenIA365ERP.Domain.Entities.Core.AssociateInboundMessage> AssociateInboundMessages => Set<IngenIA365ERP.Domain.Entities.Core.AssociateInboundMessage>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentAccount> IssuedDocumentAccounts => Set<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentAccount>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountPayoutOrder> AccountPayoutOrders => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountPayoutOrder>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.MemberAccountOperation> MemberAccountOperations => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.MemberAccountOperation>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.MemberAccountTransaction> MemberAccountTransactions => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.MemberAccountTransaction>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections.MemberAccountDailyPosition> MemberAccountDailyPositions => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections.MemberAccountDailyPosition>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections.MemberAccountControl> MemberAccountControls => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections.MemberAccountControl>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineVersion> DepositLineVersions => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineVersion>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineRateTier> DepositLineRateTiers => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineRateTier>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.ContributionLineVersion> ContributionLineVersions => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.ContributionLineVersion>();
    // Feature 014, US4 (T110, T114, T118).
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.GmfMonthlyUsage> GmfMonthlyUsages => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.GmfMonthlyUsage>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineCharge> DepositLineCharges => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineCharge>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountCharge> AccountCharges => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountCharge>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountChargeExemption> AccountChargeExemptions => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountChargeExemption>();
    public DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.DirectDebitMandate> DirectDebitMandates => Set<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.DirectDebitMandate>();

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

    /// <inheritdoc />
    public void DescartarCambiosSalvo(params object[] conservar)
    {
        ArgumentNullException.ThrowIfNull(conservar);
        // El token de concurrencia de cada una (en PostgreSQL, la sombra xmin) antes de soltar: Attach lo dejaría en su valor por defecto.
        var tokens = conservar.Distinct(ReferenceEqualityComparer.Instance).Select(entidad => (Entidad: entidad!, Valores: Entry(entidad!).Properties
                .Where(p => p.Metadata.IsConcurrencyToken).Select(p => (p.Metadata.Name, p.CurrentValue)).ToList()))
            .ToList();
        ChangeTracker.Clear();
        foreach (var (entidad, valores) in tokens)
        {
            var entrada = Attach(entidad);
            foreach (var (nombre, valor) in valores)
            {
                var propiedad = entrada.Property(nombre);
                propiedad.CurrentValue = valor;
                propiedad.OriginalValue = valor;
                propiedad.IsModified = false;
            }
        }
    }

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
