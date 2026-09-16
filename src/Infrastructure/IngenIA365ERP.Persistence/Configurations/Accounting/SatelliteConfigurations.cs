using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

// Feature 009, entrega E4 (conciliación, presupuesto, impuestos, exógena, activos). Las tablas
// se crean con la migración ContabilidadNiif de una vez, aunque sus pantallas lleguen después.

public class BankStatementColumnMapConfiguration : IEntityTypeConfiguration<BankStatementColumnMap>
{
    public void Configure(EntityTypeBuilder<BankStatementColumnMap> builder)
    {
        builder.ToTable("ACC_BankStatementColumnMaps");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_BankStatementColumnMaps_PublicId");
        builder.Property(e => e.DateFormat).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SignConvention).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Delimiter).HasMaxLength(1);
        builder.HasIndex(e => e.AccountId).IsUnique().HasDatabaseName("UK_ACC_BankStatementColumnMaps_Account").HasFilter("[IsDeleted] = 0");
        builder.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("ACC_BankReconciliations");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_BankReconciliations_PublicId");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.ClosedBy).HasMaxLength(100);
        builder.Property(e => e.ReopenedBy).HasMaxLength(100);
        builder.Property(e => e.ReopenReason).HasMaxLength(200);
        builder.HasIndex(e => new { e.AccountId, e.PeriodId }).IsUnique().HasDatabaseName("UK_ACC_BankReconciliations_Account_Period").HasFilter("[IsDeleted] = 0");
        builder.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Period).WithMany().HasForeignKey(e => e.PeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.StatementLines).WithOne(l => l.Reconciliation).HasForeignKey(l => l.ReconciliationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> builder)
    {
        builder.ToTable("ACC_BankStatementLines");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_BankStatementLines_PublicId");
        builder.Property(e => e.Reference).HasMaxLength(60);
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(e => e.MatchKind).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.MatchedBy).HasMaxLength(100);
        builder.HasIndex(e => new { e.ReconciliationId, e.Fingerprint }).IsUnique().HasDatabaseName("UK_ACC_BankStatementLines_Fingerprint");
        builder.HasIndex(e => e.JournalEntryId).IsUnique().HasDatabaseName("UK_ACC_BankStatementLines_JournalEntry").HasFilter("[JournalEntryId] IS NOT NULL");
        builder.HasOne(e => e.JournalEntry).WithMany().HasForeignKey(e => e.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DraftDocument).WithMany().HasForeignKey(e => e.DraftDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("ACC_Budgets");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_Budgets_PublicId");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(12);
        builder.Property(e => e.ApprovedBy).HasMaxLength(100);
        builder.Property(e => e.ChangeReason).HasMaxLength(200);
        builder.HasIndex(e => new { e.FiscalYearId, e.Version }).IsUnique().HasDatabaseName("UK_ACC_Budgets_Year_Version").HasFilter("[IsDeleted] = 0");
        builder.HasOne(e => e.FiscalYear).WithMany().HasForeignKey(e => e.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne(l => l.Budget).HasForeignKey(l => l.BudgetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        builder.ToTable("ACC_BudgetLines");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_BudgetLines_PublicId");
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.BudgetId, e.AccountId, e.BranchId, e.CostCenterId, e.Month }).IsUnique().HasDatabaseName("UK_ACC_BudgetLines_Key").HasFilter("[IsDeleted] = 0");
        builder.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WithholdingCertificateConfiguration : IEntityTypeConfiguration<WithholdingCertificate>
{
    public void Configure(EntityTypeBuilder<WithholdingCertificate> builder)
    {
        builder.ToTable("ACC_WithholdingCertificates");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_WithholdingCertificates_PublicId");
        builder.Property(e => e.TaxKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.IssuedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LedgerFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(e => e.LastSentTo).HasMaxLength(200);
        builder.Property(e => e.TotalBase).HasPrecision(18, 2);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.TaxKind, e.Number }).IsUnique().HasDatabaseName("UK_ACC_WithholdingCertificates_Kind_Number");
        builder.HasIndex(e => new { e.PersonId, e.TaxKind, e.Year }).HasDatabaseName("IX_ACC_WithholdingCertificates_Person_Kind_Year");
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne(l => l.Certificate).HasForeignKey(l => l.CertificateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WithholdingCertificateLineConfiguration : IEntityTypeConfiguration<WithholdingCertificateLine>
{
    public void Configure(EntityTypeBuilder<WithholdingCertificateLine> builder)
    {
        builder.ToTable("ACC_WithholdingCertificateLines");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_WithholdingCertificateLines_PublicId");
        builder.Property(e => e.ConceptCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Base).HasPrecision(18, 2);
        builder.Property(e => e.Rate).HasPrecision(9, 4);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TaxFormConfiguration : IEntityTypeConfiguration<TaxForm>
{
    public void Configure(EntityTypeBuilder<TaxForm> builder)
    {
        builder.ToTable("ACC_TaxForms");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_TaxForms_PublicId");
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_ACC_TaxForms_Code").HasFilter("[IsDeleted] = 0");
        builder.HasMany(e => e.Lines).WithOne(l => l.Form).HasForeignKey(l => l.FormId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TaxFormLineConfiguration : IEntityTypeConfiguration<TaxFormLine>
{
    public void Configure(EntityTypeBuilder<TaxFormLine> builder)
    {
        builder.ToTable("ACC_TaxFormLines");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_TaxFormLines_PublicId");
        builder.Property(e => e.LineCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Selector).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.AccountPrefixes).HasMaxLength(400).IsRequired();
    }
}

public class ExogenousFormatConfiguration : IEntityTypeConfiguration<ExogenousFormat>
{
    public void Configure(EntityTypeBuilder<ExogenousFormat> builder)
    {
        builder.ToTable("ACC_ExogenousFormats");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_ExogenousFormats_PublicId");
        builder.Property(e => e.FormatCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Origin).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.MinAmount).HasPrecision(18, 2);
        builder.Property(e => e.MinorAmountsTaxId).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => new { e.TaxYear, e.FormatCode }).IsUnique().HasDatabaseName("UK_ACC_ExogenousFormats_Year_Code").HasFilter("[IsDeleted] = 0");
        builder.HasMany(e => e.Concepts).WithOne(c => c.Format).HasForeignKey(c => c.FormatId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExogenousConceptConfiguration : IEntityTypeConfiguration<ExogenousConcept>
{
    public void Configure(EntityTypeBuilder<ExogenousConcept> builder)
    {
        builder.ToTable("ACC_ExogenousConcepts");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_ExogenousConcepts_PublicId");
        builder.Property(e => e.ConceptCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(e => new { e.FormatId, e.ConceptCode }).IsUnique().HasDatabaseName("UK_ACC_ExogenousConcepts_Format_Code").HasFilter("[IsDeleted] = 0");
        builder.HasMany(e => e.Accounts).WithOne(a => a.Concept).HasForeignKey(a => a.ConceptId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExogenousConceptAccountConfiguration : IEntityTypeConfiguration<ExogenousConceptAccount>
{
    public void Configure(EntityTypeBuilder<ExogenousConceptAccount> builder)
    {
        builder.ToTable("ACC_ExogenousConceptAccounts");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_ExogenousConceptAccounts_PublicId");
        builder.Property(e => e.ValueField).HasMaxLength(40).IsRequired();
        builder.Property(e => e.AccountPrefix).HasMaxLength(12).IsRequired();
        builder.Property(e => e.Selector).HasMaxLength(10).IsRequired();
    }
}

public class ExogenousRunConfiguration : IEntityTypeConfiguration<ExogenousRun>
{
    public void Configure(EntityTypeBuilder<ExogenousRun> builder)
    {
        builder.ToTable("ACC_ExogenousRuns");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_ExogenousRuns_PublicId");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.GeneratedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ExportedBy).HasMaxLength(100);
        builder.HasIndex(e => new { e.FormatId, e.Version }).IsUnique().HasDatabaseName("UK_ACC_ExogenousRuns_Format_Version");
        builder.HasOne(e => e.Format).WithMany().HasForeignKey(e => e.FormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne(l => l.Run).HasForeignKey(l => l.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExogenousRunLineConfiguration : IEntityTypeConfiguration<ExogenousRunLine>
{
    public void Configure(EntityTypeBuilder<ExogenousRunLine> builder)
    {
        builder.ToTable("ACC_ExogenousRunLines");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_ExogenousRunLines_PublicId");
        builder.Property(e => e.IdType).HasMaxLength(2);
        builder.Property(e => e.IdNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CheckDigit).HasMaxLength(1);
        builder.Property(e => e.Surname1).HasMaxLength(60);
        builder.Property(e => e.Surname2).HasMaxLength(60);
        builder.Property(e => e.Name1).HasMaxLength(60);
        builder.Property(e => e.Name2).HasMaxLength(60);
        builder.Property(e => e.BusinessName).HasMaxLength(150);
        builder.Property(e => e.Address).HasMaxLength(200);
        builder.Property(e => e.MunicipalityCode).HasMaxLength(5);
        builder.Property(e => e.DepartmentCode).HasMaxLength(2);
        foreach (var campo in new[] { "Value1", "Value2", "Value3", "Value4", "Value5", "Value6", "Value7", "Value8", "Value9", "Value10" })
            builder.Property(campo).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.RunId, e.PersonId }).HasDatabaseName("IX_ACC_ExogenousRunLines_Run_Person");
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Concept).WithMany().HasForeignKey(e => e.ConceptId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FixedAssetConfiguration : IEntityTypeConfiguration<FixedAsset>
{
    public void Configure(EntityTypeBuilder<FixedAsset> builder)
    {
        builder.ToTable("ACC_FixedAssets");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_FixedAssets_PublicId");
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PurchaseDocument).HasMaxLength(60);
        builder.Property(e => e.RetireReason).HasMaxLength(200);
        builder.Property(e => e.Cost).HasPrecision(18, 2);
        builder.Property(e => e.ResidualValue).HasPrecision(18, 2);
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_ACC_FixedAssets_Code").HasFilter("[IsDeleted] = 0");
        builder.HasOne(e => e.AssetAccount).WithMany().HasForeignKey(e => e.AssetAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AccumulatedAccount).WithMany().HasForeignKey(e => e.AccumulatedAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ExpenseAccount).WithMany().HasForeignKey(e => e.ExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SupplierPerson).WithMany().HasForeignKey(e => e.SupplierPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.RetireDocument).WithMany().HasForeignKey(e => e.RetireDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Installments).WithOne(i => i.Asset).HasForeignKey(i => i.AssetId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(e => e.ValorDepreciable);
    }
}

public class FixedAssetInstallmentConfiguration : IEntityTypeConfiguration<FixedAssetInstallment>
{
    public void Configure(EntityTypeBuilder<FixedAssetInstallment> builder)
    {
        builder.ToTable("ACC_FixedAssetInstallments");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_FixedAssetInstallments_PublicId");
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(e => new { e.AssetId, e.PeriodId }).IsUnique().HasDatabaseName("UK_ACC_FixedAssetInstallments_Asset_Period").HasFilter("[IsDeleted] = 0");
        builder.HasOne(e => e.Period).WithMany().HasForeignKey(e => e.PeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssetRunConfiguration : IEntityTypeConfiguration<AssetRun>
{
    public void Configure(EntityTypeBuilder<AssetRun> builder)
    {
        builder.ToTable("ACC_AssetRuns");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AssetRuns_PublicId");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.ExecutedBy).HasMaxLength(100).IsRequired();
        // Una corrida por período (FR-077): la unicidad es de la base, no una comprobación.
        builder.HasIndex(e => e.PeriodId).IsUnique().HasDatabaseName("UK_ACC_AssetRuns_Period").HasFilter("[IsDeleted] = 0");
        builder.HasOne(e => e.Period).WithMany().HasForeignKey(e => e.PeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReversalDocument).WithMany().HasForeignKey(e => e.ReversalDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
