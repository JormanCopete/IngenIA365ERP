using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Accounting;
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
    // TenantBranches salio de IApplicationDbContext: es del plano de control.

    // === Resto de la interfaz — throw on access (auth no las toca) ===
    DbSet<Person> IApplicationDbContext.People => throw new NotImplementedException();
    DbSet<Associate> IApplicationDbContext.Associates => throw new NotImplementedException();
    DbSet<Branch> IApplicationDbContext.Branches => throw new NotImplementedException();
    DbSet<CostCenter> IApplicationDbContext.CostCenters => throw new NotImplementedException();
    DbSet<City> IApplicationDbContext.Cities => throw new NotImplementedException();
    DbSet<Bank> IApplicationDbContext.Banks => throw new NotImplementedException();
    DbSet<Company> IApplicationDbContext.Companies => throw new NotImplementedException();
    DbSet<Committee> IApplicationDbContext.Committees => throw new NotImplementedException();
    DbSet<Beneficiary> IApplicationDbContext.Beneficiaries => throw new NotImplementedException();
    DbSet<Reference> IApplicationDbContext.References => throw new NotImplementedException();
    DbSet<Country> IApplicationDbContext.Countries => throw new NotImplementedException();
    DbSet<Department> IApplicationDbContext.Departments => throw new NotImplementedException();
    DbSet<Section> IApplicationDbContext.Sections => throw new NotImplementedException();
    DbSet<Profession> IApplicationDbContext.Professions => throw new NotImplementedException();
    DbSet<Position> IApplicationDbContext.Positions => throw new NotImplementedException();
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
    DbSet<ChartOfAccount> IApplicationDbContext.ChartOfAccounts => throw new NotImplementedException();
    DbSet<AccountBalance> IApplicationDbContext.AccountBalances => throw new NotImplementedException();
    DbSet<JournalEntry> IApplicationDbContext.JournalEntries => throw new NotImplementedException();
    DbSet<VoucherType> IApplicationDbContext.VoucherTypes => throw new NotImplementedException();
    DbSet<AccountingPeriod> IApplicationDbContext.AccountingPeriods => throw new NotImplementedException();
    DbSet<AccountGroup> IApplicationDbContext.AccountGroups => throw new NotImplementedException();
    DbSet<AccountSubgroup> IApplicationDbContext.AccountSubgroups => throw new NotImplementedException();
    DbSet<RiskCategory> IApplicationDbContext.RiskCategories => throw new NotImplementedException();
    DbSet<VatTaxLine> IApplicationDbContext.VatTaxLines => throw new NotImplementedException();
    DbSet<IncomeTaxLine> IApplicationDbContext.IncomeTaxLines => throw new NotImplementedException();
    DbSet<WithholdingTaxLine> IApplicationDbContext.WithholdingTaxLines => throw new NotImplementedException();
    DbSet<IcaTaxLine> IApplicationDbContext.IcaTaxLines => throw new NotImplementedException();
    DbSet<GmfTaxLine> IApplicationDbContext.GmfTaxLines => throw new NotImplementedException();
    DbSet<DianReportFormat> IApplicationDbContext.DianReportFormats => throw new NotImplementedException();
    DbSet<TaxFormCode> IApplicationDbContext.TaxFormCodes => throw new NotImplementedException();
    DbSet<AccountingDocument> IApplicationDbContext.AccountingDocuments => throw new NotImplementedException();
    DbSet<JournalEntryItem> IApplicationDbContext.JournalEntryItems => throw new NotImplementedException();
    DbSet<AuxiliaryDocument> IApplicationDbContext.AuxiliaryDocuments => throw new NotImplementedException();
    DbSet<ThirdPartyAccount> IApplicationDbContext.ThirdPartyAccounts => throw new NotImplementedException();
    DbSet<BankReconciliation> IApplicationDbContext.BankReconciliations => throw new NotImplementedException();
    DbSet<BankReconciliationMaster> IApplicationDbContext.BankReconciliationMasters => throw new NotImplementedException();
    DbSet<Amortization> IApplicationDbContext.Amortizations => throw new NotImplementedException();
    DbSet<Depreciation> IApplicationDbContext.Depreciations => throw new NotImplementedException();
    DbSet<Budget> IApplicationDbContext.Budgets => throw new NotImplementedException();
    DbSet<LoanPortfolio> IApplicationDbContext.LoanPortfolios => throw new NotImplementedException();
    DbSet<LendingTransaction> IApplicationDbContext.LendingTransactions => throw new NotImplementedException();
    DbSet<PendingInstallment> IApplicationDbContext.PendingInstallments => throw new NotImplementedException();
    DbSet<CreditLineParameter> IApplicationDbContext.CreditLineParameters => throw new NotImplementedException();
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
    DbSet<PayrollDeductionEntry> IApplicationDbContext.PayrollDeductionEntries => throw new NotImplementedException();
    DbSet<WithdrawalStatus> IApplicationDbContext.WithdrawalStatuses => throw new NotImplementedException();
    DbSet<HousingParameter> IApplicationDbContext.HousingParameters => throw new NotImplementedException();
    DbSet<SiplaParameter> IApplicationDbContext.SiplaParameters => throw new NotImplementedException();
    DbSet<PeriodicityParameter> IApplicationDbContext.PeriodicityParameters => throw new NotImplementedException();
    DbSet<TermRate> IApplicationDbContext.TermRates => throw new NotImplementedException();
    DbSet<PortfolioAccount> IApplicationDbContext.PortfolioAccounts => throw new NotImplementedException();
    DbSet<ContributionReduction> IApplicationDbContext.ContributionReductions => throw new NotImplementedException();
    DbSet<AssociateWithdrawal> IApplicationDbContext.AssociateWithdrawals => throw new NotImplementedException();
    DbSet<CertificateEntry> IApplicationDbContext.CertificateEntries => throw new NotImplementedException();
    DbSet<Employee> IApplicationDbContext.Employees => throw new NotImplementedException();
    DbSet<PayrollConcept> IApplicationDbContext.PayrollConcepts => throw new NotImplementedException();
    DbSet<PayPeriod> IApplicationDbContext.PayPeriods => throw new NotImplementedException();
    DbSet<HealthInsuranceProvider> IApplicationDbContext.HealthInsuranceProviders => throw new NotImplementedException();
    DbSet<WorkRiskProvider> IApplicationDbContext.WorkRiskProviders => throw new NotImplementedException();
    DbSet<WorkRiskRate> IApplicationDbContext.WorkRiskRates => throw new NotImplementedException();
    DbSet<PensionProvider> IApplicationDbContext.PensionProviders => throw new NotImplementedException();
    DbSet<SeveranceProvider> IApplicationDbContext.SeveranceProviders => throw new NotImplementedException();
    DbSet<ConceptAccount> IApplicationDbContext.ConceptAccounts => throw new NotImplementedException();
    DbSet<WithholdingParameter> IApplicationDbContext.WithholdingParameters => throw new NotImplementedException();
    DbSet<WithholdingCause> IApplicationDbContext.WithholdingCauses => throw new NotImplementedException();
    DbSet<AutoContributionParam> IApplicationDbContext.AutoContributionParams => throw new NotImplementedException();
    DbSet<PayrollTransaction> IApplicationDbContext.PayrollTransactions => throw new NotImplementedException();
    DbSet<PayrollEntry> IApplicationDbContext.PayrollEntries => throw new NotImplementedException();
    DbSet<SalaryChange> IApplicationDbContext.SalaryChanges => throw new NotImplementedException();
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
    DbSet<Salesperson> IApplicationDbContext.Salespeople => throw new NotImplementedException();
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
        modelBuilder.Ignore<Person>();
        modelBuilder.Ignore<City>();
        modelBuilder.Ignore<Spouse>();
        modelBuilder.Ignore<Subscription>();
        modelBuilder.Ignore<TenantSetting>();
        modelBuilder.Ignore<TenantBranch>();

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
