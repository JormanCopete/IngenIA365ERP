using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

/// <summary>Feature 009. Los enums contables se guardan por nombre (legibles en la base y en la auditoría).</summary>
public class AccountingSetupConfiguration : IEntityTypeConfiguration<AccountingSetup>
{
    public void Configure(EntityTypeBuilder<AccountingSetup> builder)
    {
        builder.ToTable("ACC_AccountingSetups");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AccountingSetups_PublicId");

        builder.Property(e => e.TaxTolerance).HasPrecision(18, 2);
        builder.Property(e => e.InitializedBy).HasMaxLength(100).IsRequired();

        builder.HasOne(e => e.Catalog).WithMany().HasForeignKey(e => e.CatalogId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ResultAccount).WithMany().HasForeignKey(e => e.ResultAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.MainBranch).WithMany().HasForeignKey(e => e.MainBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.OpeningDocument).WithMany().HasForeignKey(e => e.OpeningDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountCatalogConfiguration : IEntityTypeConfiguration<AccountCatalog>
{
    public void Configure(EntityTypeBuilder<AccountCatalog> builder)
    {
        builder.ToTable("ACC_AccountCatalogs");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AccountCatalogs_PublicId");

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Version).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ImportedBy).HasMaxLength(100);
        builder.Property(e => e.ValidatedBy).HasMaxLength(100);

        // Único sólo entre no eliminados: un catálogo propio se puede retirar y volver a importar (U1).
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_ACC_AccountCatalogs_Code").HasFilter("[IsDeleted] = 0");
    }
}

public class AccountCatalogEntryConfiguration : IEntityTypeConfiguration<AccountCatalogEntry>
{
    public void Configure(EntityTypeBuilder<AccountCatalogEntry> builder)
    {
        builder.ToTable("ACC_AccountCatalogEntries");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AccountCatalogEntries_PublicId");

        builder.Property(e => e.Code).HasMaxLength(6).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Nature).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.NiifItemCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ParentCode).HasMaxLength(6);

        builder.HasIndex(e => new { e.CatalogId, e.Code }).IsUnique().HasDatabaseName("UK_ACC_AccountCatalogEntries_Catalog_Code");
        builder.HasOne(e => e.Catalog).WithMany(c => c.Entries).HasForeignKey(e => e.CatalogId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FinancialStatementItemConfiguration : IEntityTypeConfiguration<FinancialStatementItem>
{
    public void Configure(EntityTypeBuilder<FinancialStatementItem> builder)
    {
        builder.ToTable("ACC_FinancialStatementItems");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_FinancialStatementItems_PublicId");

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Statement).HasConversion<string>().HasMaxLength(24);
        builder.Property(e => e.Section).HasMaxLength(80).IsRequired();
        builder.Property(e => e.ParentCode).HasMaxLength(20);

        builder.HasIndex(e => new { e.NiifGroup, e.Code }).IsUnique().HasDatabaseName("UK_ACC_FinancialStatementItems_Group_Code");
    }
}
