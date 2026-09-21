using IngenIA365ERP.Application.Common.Interfaces;
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

namespace IngenIA365ERP.Application.Tests.Common;

/// <summary>
/// DbContext en memoria limitado a las entidades que los handlers de US1 tocan.
/// El resto de la superficie de <see cref="IApplicationDbContext"/> se materializa
/// con lazy <see cref="LazyDbSet{T}"/> que ignoran al modelo si jamás se acceden,
/// permitiendo a InMemory descubrir solo entidades simples sin colisión.
/// </summary>
public sealed class TestApplicationDbContext : Microsoft.EntityFrameworkCore.DbContext, IApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options) : base(options) { }

    public void DescartarCambios() => ChangeTracker.Clear();

    // === Auth-related, sí registradas ===
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<UserTenantAssignment> UserTenantAssignments => Set<UserTenantAssignment>();
    public DbSet<UserBranchAssignment> UserBranchAssignments => Set<UserBranchAssignment>();
    public DbSet<PasswordPolicy> PasswordPolicies => Set<PasswordPolicy>();
    public DbSet<PasswordHistory> PasswordHistory => Set<PasswordHistory>();
    public DbSet<MfaBackupCode> MfaBackupCodes => Set<MfaBackupCode>();
    public DbSet<MfaResetRequest> MfaResetRequests => Set<MfaResetRequest>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDeliveryFailure> NotificationDeliveryFailures => Set<NotificationDeliveryFailure>();
    public DbSet<HabeasDataPolicyVersion> HabeasDataPolicyVersions => Set<HabeasDataPolicyVersion>();
    public DbSet<HabeasDataConsent> HabeasDataConsents => Set<HabeasDataConsent>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    // === Nomina (feature 005): registradas para probar handlers de novedades y liquidacion ===
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WorkRiskRate> WorkRiskRates => Set<WorkRiskRate>();
    public DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
    public DbSet<SalaryChange> SalaryChanges => Set<SalaryChange>();
    public DbSet<PayrollPlan> PayrollPlans => Set<PayrollPlan>();
    public DbSet<PayrollConceptDefinition> PayrollConceptDefinitions => Set<PayrollConceptDefinition>();
    public DbSet<PayrollConceptDefinitionAccount> PayrollConceptDefinitionAccounts => Set<PayrollConceptDefinitionAccount>();
    public DbSet<PayrollLegalParameter> PayrollLegalParameters => Set<PayrollLegalParameter>();
    public DbSet<WithholdingParameter> WithholdingParameters => Set<WithholdingParameter>();
    public DbSet<PayrollLegalParameterRange> PayrollLegalParameterRanges => Set<PayrollLegalParameterRange>();
    public DbSet<PayrollNovelty> PayrollNovelties => Set<PayrollNovelty>();
    public DbSet<PayrollRecurringNovelty> PayrollRecurringNovelties => Set<PayrollRecurringNovelty>();
    public DbSet<EmployeeWithholdingRate> EmployeeWithholdingRates => Set<EmployeeWithholdingRate>();
    public DbSet<EmployeeTaxDeduction> EmployeeTaxDeductions => Set<EmployeeTaxDeduction>();
    public DbSet<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRun> PayrollRuns => Set<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRun>();
    public DbSet<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRunEmployee> PayrollRunEmployees => Set<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRunEmployee>();
    public DbSet<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRunLine> PayrollRunLines => Set<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRunLine>();
    public DbSet<PayrollPayment> PayrollPayments => Set<PayrollPayment>();
    public DbSet<PayslipDelivery> PayslipDeliveries => Set<PayslipDelivery>();
    // === Nomina (feature 010, entrega N1): liquidaciones especiales y su parametrizacion ===
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
    // TenantBranches salio de IApplicationDbContext: es del plano de control.

    // === Resto de la interfaz — throw on access (auth no las toca) ===
    public DbSet<Person> People => Set<Person>();
    // Feature 008: asociados, ciudades y vendedores entran al modelo para probar las fábricas
    // de persona (duplicado, ciudad inexistente) y la reconciliación de banderas al restaurar.
    public DbSet<Associate> Associates => Set<Associate>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<HealthInsuranceProvider> HealthInsuranceProviders => Set<HealthInsuranceProvider>();
    DbSet<Company> IApplicationDbContext.Companies => throw new NotImplementedException();
    DbSet<Committee> IApplicationDbContext.Committees => throw new NotImplementedException();
    DbSet<Beneficiary> IApplicationDbContext.Beneficiaries => throw new NotImplementedException();
    DbSet<Reference> IApplicationDbContext.References => throw new NotImplementedException();
    DbSet<Country> IApplicationDbContext.Countries => throw new NotImplementedException();
    DbSet<Department> IApplicationDbContext.Departments => throw new NotImplementedException();
    DbSet<Section> IApplicationDbContext.Sections => throw new NotImplementedException();
    DbSet<Profession> IApplicationDbContext.Professions => throw new NotImplementedException();
    public DbSet<Position> Positions => Set<Position>();
    DbSet<Relationship> IApplicationDbContext.Relationships => throw new NotImplementedException();
    DbSet<WithdrawalReason> IApplicationDbContext.WithdrawalReasons => throw new NotImplementedException();
    DbSet<Sport> IApplicationDbContext.Sports => throw new NotImplementedException();
    DbSet<CulturalActivity> IApplicationDbContext.CulturalActivities => throw new NotImplementedException();
    DbSet<Disease> IApplicationDbContext.Diseases => throw new NotImplementedException();
    DbSet<ExternalEntity> IApplicationDbContext.ExternalEntities => throw new NotImplementedException();
    DbSet<Agreement> IApplicationDbContext.Agreements => throw new NotImplementedException();
    DbSet<Course> IApplicationDbContext.Courses => throw new NotImplementedException();
    DbSet<Spouse> IApplicationDbContext.Spouses => throw new NotImplementedException();
    DbSet<PersonFinancial> IApplicationDbContext.PeopleFinancial => throw new NotImplementedException();
    DbSet<AssociateCategory> IApplicationDbContext.AssociateCategories => throw new NotImplementedException();
    DbSet<CommitteeMember> IApplicationDbContext.CommitteeMembers => throw new NotImplementedException();
    DbSet<EmployerCompany> IApplicationDbContext.EmployerCompanies => throw new NotImplementedException();
    DbSet<Advisor> IApplicationDbContext.Advisors => throw new NotImplementedException();
    DbSet<PaymentMethod> IApplicationDbContext.PaymentMethods => throw new NotImplementedException();
    // Feature 009: el contabilizador lee configuración, plan, tipos y períodos, y escribe documentos y líneas.
    public DbSet<AccountingSetup> AccountingSetups => Set<AccountingSetup>();
    public DbSet<AccountCatalog> AccountCatalogs => Set<AccountCatalog>();
    public DbSet<AccountCatalogEntry> AccountCatalogEntries => Set<AccountCatalogEntry>();
    public DbSet<FinancialStatementItem> FinancialStatementItems => Set<FinancialStatementItem>();
    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();
    public DbSet<AccountTaxRate> AccountTaxRates => Set<AccountTaxRate>();
    public DbSet<CrossDocumentType> CrossDocumentTypes => Set<CrossDocumentType>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    DbSet<BankStatementColumnMap> IApplicationDbContext.BankStatementColumnMaps => throw new NotImplementedException();
    DbSet<BankStatementLine> IApplicationDbContext.BankStatementLines => throw new NotImplementedException();
    DbSet<BudgetLine> IApplicationDbContext.BudgetLines => throw new NotImplementedException();
    DbSet<WithholdingCertificate> IApplicationDbContext.WithholdingCertificates => throw new NotImplementedException();
    DbSet<WithholdingCertificateLine> IApplicationDbContext.WithholdingCertificateLines => throw new NotImplementedException();
    DbSet<TaxForm> IApplicationDbContext.TaxForms => throw new NotImplementedException();
    DbSet<TaxFormLine> IApplicationDbContext.TaxFormLines => throw new NotImplementedException();
    DbSet<ExogenousFormat> IApplicationDbContext.ExogenousFormats => throw new NotImplementedException();
    DbSet<ExogenousConcept> IApplicationDbContext.ExogenousConcepts => throw new NotImplementedException();
    DbSet<ExogenousConceptAccount> IApplicationDbContext.ExogenousConceptAccounts => throw new NotImplementedException();
    DbSet<ExogenousRun> IApplicationDbContext.ExogenousRuns => throw new NotImplementedException();
    DbSet<ExogenousRunLine> IApplicationDbContext.ExogenousRunLines => throw new NotImplementedException();
    DbSet<FixedAsset> IApplicationDbContext.FixedAssets => throw new NotImplementedException();
    DbSet<FixedAssetInstallment> IApplicationDbContext.FixedAssetInstallments => throw new NotImplementedException();
    DbSet<AssetRun> IApplicationDbContext.AssetRuns => throw new NotImplementedException();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<VoucherType> VoucherTypes => Set<VoucherType>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<AccountingDocument> AccountingDocuments => Set<AccountingDocument>();
    // La reapertura de un período deja «desactualizadas» las conciliaciones cerradas del mes (US3); las líneas del extracto son de E3.
    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();
    DbSet<Budget> IApplicationDbContext.Budgets => throw new NotImplementedException();
    // Feature 010: la definitiva lee Cartera por persona (FR-018a) y, al aprobar, recauda de verdad
    // (RecaudoDeCredito: cuotas pendientes, transacción RC y comprobante por el contrato).
    public DbSet<LoanPortfolio> LoanPortfolios => Set<LoanPortfolio>();
    public DbSet<LendingTransaction> LendingTransactions => Set<LendingTransaction>();
    public DbSet<PendingInstallment> PendingInstallments => Set<PendingInstallment>();
    public DbSet<CreditLineParameter> CreditLineParameters => Set<CreditLineParameter>();
    DbSet<TransactionCode> IApplicationDbContext.TransactionCodes => throw new NotImplementedException();
    DbSet<SavingsParameter> IApplicationDbContext.SavingsParameters => throw new NotImplementedException();
    DbSet<InterestRate> IApplicationDbContext.InterestRates => throw new NotImplementedException();
    DbSet<ProvisionParameter> IApplicationDbContext.ProvisionParameters => throw new NotImplementedException();
    DbSet<Zone> IApplicationDbContext.Zones => throw new NotImplementedException();
    DbSet<SubZone> IApplicationDbContext.SubZones => throw new NotImplementedException();
    DbSet<ZoneType> IApplicationDbContext.ZoneTypes => throw new NotImplementedException();
    DbSet<ApplicationStatus> IApplicationDbContext.ApplicationStatuses => throw new NotImplementedException();
    DbSet<ScoringParameter> IApplicationDbContext.ScoringParameters => throw new NotImplementedException();
    DbSet<ScoringRange> IApplicationDbContext.ScoringRanges => throw new NotImplementedException();
    DbSet<PayrollDeductionConcept> IApplicationDbContext.PayrollDeductionConcepts => throw new NotImplementedException();
    DbSet<LoanApplication> IApplicationDbContext.LoanApplications => throw new NotImplementedException();
    DbSet<DefaultRecord> IApplicationDbContext.DefaultRecords => throw new NotImplementedException();
    DbSet<CollectionCase> IApplicationDbContext.CollectionCases => throw new NotImplementedException();
    DbSet<Guarantee> IApplicationDbContext.Guarantees => throw new NotImplementedException();
    DbSet<ExtraPayment> IApplicationDbContext.ExtraPayments => throw new NotImplementedException();
    DbSet<SavingsAccount> IApplicationDbContext.SavingsAccounts => throw new NotImplementedException();
    DbSet<DepositEntry> IApplicationDbContext.DepositEntries => throw new NotImplementedException();
    DbSet<AccrualEntry> IApplicationDbContext.AccrualEntries => throw new NotImplementedException();
    DbSet<PortfolioClassification> IApplicationDbContext.PortfolioClassifications => throw new NotImplementedException();
    DbSet<RiskAssessment> IApplicationDbContext.RiskAssessments => throw new NotImplementedException();
    public DbSet<PayrollDeductionEntry> PayrollDeductionEntries => Set<PayrollDeductionEntry>();
    DbSet<WithdrawalStatus> IApplicationDbContext.WithdrawalStatuses => throw new NotImplementedException();
    DbSet<HousingParameter> IApplicationDbContext.HousingParameters => throw new NotImplementedException();
    DbSet<SiplaParameter> IApplicationDbContext.SiplaParameters => throw new NotImplementedException();
    DbSet<PeriodicityParameter> IApplicationDbContext.PeriodicityParameters => throw new NotImplementedException();
    DbSet<TermRate> IApplicationDbContext.TermRates => throw new NotImplementedException();
    DbSet<PortfolioAccount> IApplicationDbContext.PortfolioAccounts => throw new NotImplementedException();
    DbSet<ContributionReduction> IApplicationDbContext.ContributionReductions => throw new NotImplementedException();
    DbSet<AssociateWithdrawal> IApplicationDbContext.AssociateWithdrawals => throw new NotImplementedException();
    DbSet<CertificateEntry> IApplicationDbContext.CertificateEntries => throw new NotImplementedException();
    public DbSet<PayrollConcept> PayrollConcepts => Set<PayrollConcept>();
    public DbSet<WorkRiskProvider> WorkRiskProviders => Set<WorkRiskProvider>();
    public DbSet<PensionProvider> PensionProviders => Set<PensionProvider>();
    public DbSet<SeveranceProvider> SeveranceProviders => Set<SeveranceProvider>();
    public DbSet<FamilyCompensationFund> FamilyCompensationFunds => Set<FamilyCompensationFund>();
    DbSet<ConceptAccount> IApplicationDbContext.ConceptAccounts => throw new NotImplementedException();
    DbSet<WithholdingCause> IApplicationDbContext.WithholdingCauses => throw new NotImplementedException();
    DbSet<AutoContributionParam> IApplicationDbContext.AutoContributionParams => throw new NotImplementedException();
    // Feature 008: el detalle de la ficha lista movimientos recientes; entra vacío para poder probar by-person.
    public DbSet<PayrollTransaction> PayrollTransactions => Set<PayrollTransaction>();
    DbSet<PayrollEntry> IApplicationDbContext.PayrollEntries => throw new NotImplementedException();
    DbSet<Absence> IApplicationDbContext.Absences => throw new NotImplementedException();
    DbSet<TaxCertificate> IApplicationDbContext.TaxCertificates => throw new NotImplementedException();
    DbSet<Product> IApplicationDbContext.Products => throw new NotImplementedException();
    DbSet<ProductGroup> IApplicationDbContext.ProductGroups => throw new NotImplementedException();
    DbSet<PrimaryGroup> IApplicationDbContext.PrimaryGroups => throw new NotImplementedException();
    DbSet<SecondaryGroup> IApplicationDbContext.SecondaryGroups => throw new NotImplementedException();
    DbSet<InventoryTransactionType> IApplicationDbContext.InventoryTransactionTypes => throw new NotImplementedException();
    DbSet<Warehouse> IApplicationDbContext.Warehouses => throw new NotImplementedException();
    DbSet<Location> IApplicationDbContext.Locations => throw new NotImplementedException();
    DbSet<SalesPoint> IApplicationDbContext.SalesPoints => throw new NotImplementedException();
    DbSet<Shift> IApplicationDbContext.Shifts => throw new NotImplementedException();
    public DbSet<Salesperson> Salespeople => Set<Salesperson>();
    DbSet<DiscountType> IApplicationDbContext.DiscountTypes => throw new NotImplementedException();
    DbSet<PriceListType> IApplicationDbContext.PriceListTypes => throw new NotImplementedException();
    DbSet<ProductAccount> IApplicationDbContext.ProductAccounts => throw new NotImplementedException();
    DbSet<VatAccount> IApplicationDbContext.VatAccounts => throw new NotImplementedException();
    DbSet<CommissionParameter> IApplicationDbContext.CommissionParameters => throw new NotImplementedException();
    DbSet<InventoryDocument> IApplicationDbContext.InventoryDocuments => throw new NotImplementedException();
    DbSet<InventoryTransaction> IApplicationDbContext.InventoryTransactions => throw new NotImplementedException();
    DbSet<InventoryInvoice> IApplicationDbContext.InventoryInvoices => throw new NotImplementedException();
    DbSet<PhysicalInventory> IApplicationDbContext.PhysicalInventories => throw new NotImplementedException();
    DbSet<Certificate> IApplicationDbContext.Certificates => throw new NotImplementedException();
    DbSet<CdtParameter> IApplicationDbContext.CdtParameters => throw new NotImplementedException();
    DbSet<CdtRateByTerm> IApplicationDbContext.CdtRatesByTerm => throw new NotImplementedException();
    DbSet<DebitCard> IApplicationDbContext.DebitCards => throw new NotImplementedException();
    DbSet<DebitAgreementParameter> IApplicationDbContext.DebitAgreementParameters => throw new NotImplementedException();
    DbSet<PosTerminal> IApplicationDbContext.PosTerminals => throw new NotImplementedException();
    DbSet<DebitDailyParameter> IApplicationDbContext.DebitDailyParameters => throw new NotImplementedException();
    DbSet<DebitTransaction> IApplicationDbContext.DebitTransactions => throw new NotImplementedException();
    DbSet<DebitAgreement> IApplicationDbContext.DebitAgreements => throw new NotImplementedException();
    DbSet<Check> IApplicationDbContext.Checks => throw new NotImplementedException();
    DbSet<TreasuryConcept> IApplicationDbContext.TreasuryConcepts => throw new NotImplementedException();
    DbSet<TreasuryInvoice> IApplicationDbContext.TreasuryInvoices => throw new NotImplementedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ignorar entidades transitivamente alcanzables que no necesitamos.
        // Feature 008: City sí entra (PersonFactory valida CityPublicId); lo que cuelga de ella no.
        modelBuilder.Ignore<Department>();
        modelBuilder.Ignore<Spouse>();
        modelBuilder.Ignore<Subscription>();
        modelBuilder.Ignore<TenantSetting>();
        modelBuilder.Ignore<TenantBranch>();
        modelBuilder.Ignore<BankStatementLine>();

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(b =>
        {
            b.Ignore(u => u.Person);
            b.Ignore("RowVersion");
            b.HasMany(u => u.Roles).WithMany(r => r.Users);
        });
        modelBuilder.Entity<Role>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<RefreshToken>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<LoginAttempt>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<UserTenantAssignment>(b => { b.Ignore(a => a.Tenant); b.Ignore(a => a.User); b.Ignore("RowVersion"); });
        modelBuilder.Entity<UserBranchAssignment>(b => { b.Ignore(a => a.Tenant); b.Ignore(a => a.User); b.Ignore(a => a.Branch); b.Ignore("RowVersion"); });
        modelBuilder.Entity<PasswordPolicy>(b => { b.Ignore(p => p.Tenant); b.Ignore("RowVersion"); });
        modelBuilder.Entity<PasswordHistory>(b => b.Ignore(h => h.User));
        modelBuilder.Entity<MfaBackupCode>(b => { b.Ignore(c => c.User); b.Ignore("RowVersion"); });
        modelBuilder.Entity<MfaResetRequest>(b => { b.Ignore(r => r.User); b.Ignore("RowVersion"); });
        modelBuilder.Entity<Tenant>(b => { b.Ignore(t => t.Subscriptions); b.Ignore(t => t.Settings); b.Ignore("RowVersion"); });
        modelBuilder.Entity<Attachment>(b => b.Ignore("RowVersion"));

        // Nomina (feature 005): las cuentas contables y los centros de costo no hacen
        // falta para probar los handlers; el empleado se prueba sin su Person.
        // Feature 008: Employee.Person ya no se ignora — el alta en un paso enlaza la ficha a una
        // persona sin Id por navegación y la prueba tiene que ver el PersonId resuelto.
        modelBuilder.Entity<Employee>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<Associate>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<Salesperson>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollTransaction>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollConcept>(b => b.Ignore("RowVersion"));
        // City.People choca con las dos navegaciones Person→City (City y MailingCity); aquí no hace falta.
        modelBuilder.Entity<City>(b => { b.Ignore(c => c.People); b.Ignore(c => c.Beneficiaries); b.Ignore(c => c.References); b.Ignore("RowVersion"); });
        // Feature 005: lo que el cargador de insumos y el contabilizador leen. ChartOfAccount
        // sigue fuera del modelo (su grafo arrastra medio dominio): las navegaciones hacia el
        // se ignoran y las cuentas se referencian por Id.
        modelBuilder.Entity<Person>(b =>
        {
            b.Ignore(x => x.Beneficiaries); b.Ignore(x => x.References); b.Ignore(x => x.CommitteeMemberships); b.Ignore("RowVersion");
            // Feature 008: dos navegaciones a City; se fija la de residencia y se ignora la de correspondencia.
            b.HasOne(x => x.City).WithMany().HasForeignKey(x => x.CityId);
            b.Ignore(x => x.MailingCity);
        });
        modelBuilder.Entity<Branch>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<Bank>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<Position>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<CostCenter>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<VoucherType>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<AccountingPeriod>(b => b.Ignore("RowVersion"));
        // Feature 009: las dos referencias de reversion son relaciones distintas (original -> reverso y
        // reverso -> original); sin esto la convencion las empareja como uno a uno y el modelo no valida.
        modelBuilder.Entity<AccountingDocument>(b =>
        {
            b.Ignore("RowVersion");
            b.HasOne(d => d.ReversesDocument).WithMany().HasForeignKey(d => d.ReversesDocumentId);
            b.HasOne(d => d.ReversedByDocument).WithMany().HasForeignKey(d => d.ReversedByDocumentId);
            b.HasMany(d => d.Lines).WithOne(l => l.Document).HasForeignKey(l => l.DocumentId);
        });
        modelBuilder.Entity<JournalEntry>(b => b.Ignore("RowVersion"));
        // Feature 009: el plan de cuentas entra al modelo (el contabilizador lo lee y fija FirstMovementAt).
        modelBuilder.Entity<AccountingSetup>(b => b.Ignore("RowVersion"));
        // Feature 009: entidades institucionales con su persona vinculada (parametrizaciones invalidas).
        modelBuilder.Entity<WorkRiskProvider>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PensionProvider>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<SeveranceProvider>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<FamilyCompensationFund>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<AccountCatalog>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<AccountCatalogEntry>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<FinancialStatementItem>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<ChartOfAccount>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<AccountTaxRate>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<CrossDocumentType>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<FiscalYear>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollDeductionEntry>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayPeriod>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<SalaryChange>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollPlan>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollConceptDefinition>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollConceptDefinitionAccount>(b =>
        {
            b.Ignore(a => a.CostCenter); b.Ignore(a => a.DebitAccount); b.Ignore(a => a.CreditAccount); b.Ignore("RowVersion");
        });
        modelBuilder.Entity<PayrollLegalParameter>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollLegalParameterRange>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollNovelty>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollRecurringNovelty>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<EmployeeWithholdingRate>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<EmployeeTaxDeduction>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRun>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRunEmployee>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRunLine>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayrollPayment>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PayslipDelivery>(b => b.Ignore("RowVersion"));

        // Feature 010: corridas con Kind y las tablas de N1. BankAccountType es el alias en Domain de
        // PayrollBankAccountType (misma columna en la base); aqui tambien se ignora para no duplicarla.
        modelBuilder.Entity<Employee>(b => b.Ignore(e => e.BankAccountType));
        modelBuilder.Entity<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRun>(b =>
        {
            b.Ignore(r => r.EsEspecial); b.Ignore(r => r.EsCoherente); b.Ignore(r => r.SourceTypeName);
        });
        modelBuilder.Entity<CompanyPolicy>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<Holiday>(b => { b.Ignore(h => h.EsSembrado); b.Ignore("RowVersion"); });
        modelBuilder.Entity<EmployeeBenefitOpeningBalance>(b => { b.Ignore(x => x.EsEditable); b.Ignore("RowVersion"); });
        modelBuilder.Entity<VacationMovement>(b =>
        {
            b.Ignore(x => x.EstaVivo); b.Ignore("RowVersion");
            // Como en VacationMovementConfiguration (US4): la corrida navega al movimiento que la originó y el
            // movimiento apunta a la corrida que lo liquidó SIN navegación. Por convención, con una FK candidata a
            // cada lado, EF emparejaba PayrollRun.VacationMovement con VacationMovement.PayrollRunId y al guardar
            // la corrida nueva dejaba VacationMovementId en nulo.
            b.HasOne<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId);
        });
        modelBuilder.Entity<IngenIA365ERP.Domain.Entities.Payroll.Transactions.PayrollRun>(b =>
            b.HasOne(r => r.VacationMovement).WithMany().HasForeignKey(r => r.VacationMovementId));
        modelBuilder.Entity<TerminationReason>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<EmploymentTermination>(b => { b.Ignore(x => x.EstaViva); b.Ignore("RowVersion"); });
        modelBuilder.Entity<SettlementDeduction>(b => { b.Ignore(x => x.FueAjustado); b.Ignore("RowVersion"); });
        modelBuilder.Entity<WithholdingRateCalculation>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<WithholdingRateCalculationMonth>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<SeveranceFundDeposit>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<LoanPortfolio>(b =>
        {
            b.Ignore(l => l.Transactions); b.Ignore(l => l.PendingInstallments);
            b.Ignore(l => l.ExtraPayments); b.Ignore(l => l.Guarantees); b.Ignore(l => l.DefaultRecords); b.Ignore("RowVersion");
            // El recaudo real incluye la persona y la línea del crédito (ProcessPayment / RecaudoDeCredito).
            b.HasOne(l => l.Person).WithMany().HasForeignKey(l => l.PersonId);
            b.HasOne(l => l.CreditLine).WithMany(c => c.LoanPortfolios).HasForeignKey(l => l.CreditLineId);
        });
        modelBuilder.Entity<CreditLineParameter>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<LendingTransaction>(b => { b.Ignore("RowVersion"); b.HasOne(t => t.CreditLine).WithMany().HasForeignKey(t => t.CreditLineId); });
        modelBuilder.Entity<PendingInstallment>(b => { b.Ignore("RowVersion"); b.HasOne(i => i.CreditLine).WithMany().HasForeignKey(i => i.CreditLineId); });
    }
}

public static class TestDbContextFactory
{
    public static TestApplicationDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new TestApplicationDbContext(options);
        ctx.PasswordPolicies.Add(new PasswordPolicy
        {
            TenantId = null,
            MinLength = 8,
            ExpiryDays = 90,
            HistorySize = 5,
            LockoutThreshold = 5,
            LockoutMinutes = 15
        });
        ctx.SaveChanges();
        return ctx;
    }
}
