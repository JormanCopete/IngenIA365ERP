using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

/// <summary>
/// Feature 009. El número es único por tipo sólo cuando existe (los borradores no lo tienen);
/// ese índice es lo que hace imposible el duplicado bajo concurrencia (R3).
/// </summary>
public class AccountingDocumentConfiguration : IEntityTypeConfiguration<AccountingDocument>
{
    public void Configure(EntityTypeBuilder<AccountingDocument> builder)
    {
        builder.ToTable("ACC_Documents");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_Documents_PublicId");

        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.OriginModule).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SourceType).HasMaxLength(60);
        builder.Property(e => e.RegisteredBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PostedBy).HasMaxLength(100);
        builder.Property(e => e.ReversalReason).HasMaxLength(200);
        builder.Property(e => e.TotalDebit).HasPrecision(18, 2);
        builder.Property(e => e.TotalCredit).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.VoucherTypeId, e.Number }).IsUnique().HasDatabaseName("UK_ACC_Documents_Type_Number").HasFilter("[Number] IS NOT NULL");
        builder.HasIndex(e => e.Date).HasDatabaseName("IX_ACC_Documents_Date");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_ACC_Documents_Status");
        builder.HasIndex(e => e.PeriodId).HasDatabaseName("IX_ACC_Documents_Period");
        builder.HasIndex(e => new { e.OriginModule, e.SourcePublicId }).HasDatabaseName("IX_ACC_Documents_Origin_Source");

        builder.HasOne(e => e.VoucherType).WithMany().HasForeignKey(e => e.VoucherTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Period).WithMany().HasForeignKey(e => e.PeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReversesDocument).WithMany().HasForeignKey(e => e.ReversesDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReversedByDocument).WithMany().HasForeignKey(e => e.ReversedByDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne(l => l.Document).HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.EsDeModulo);
        builder.Ignore(e => e.EstaCuadrado);
    }
}

/// <summary>Índices de lectura: todos los saldos son sumas sobre esta tabla (R4).</summary>
public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("ACC_JournalEntries");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_JournalEntries_PublicId");

        builder.Property(e => e.CrossDocumentNumber).HasMaxLength(30);
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.Debit).HasPrecision(18, 2);
        builder.Property(e => e.Credit).HasPrecision(18, 2);
        builder.Property(e => e.TaxBase).HasPrecision(18, 2);

        builder.HasIndex(e => e.DocumentId).HasDatabaseName("IX_ACC_JournalEntries_Document");
        builder.HasIndex(e => new { e.AccountId, e.Date }).HasDatabaseName("IX_ACC_JournalEntries_Account_Date");
        builder.HasIndex(e => new { e.PersonId, e.AccountId, e.Date }).HasDatabaseName("IX_ACC_JournalEntries_Person_Account_Date");
        builder.HasIndex(e => new { e.BranchId, e.Date }).HasDatabaseName("IX_ACC_JournalEntries_Branch_Date");
        builder.HasIndex(e => new { e.CostCenterId, e.Date }).HasDatabaseName("IX_ACC_JournalEntries_CostCenter_Date");
        builder.HasIndex(e => new { e.CrossDocumentTypeId, e.CrossDocumentNumber, e.PersonId }).HasDatabaseName("IX_ACC_JournalEntries_CrossDocument");

        builder.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CrossDocumentType).WithMany().HasForeignKey(e => e.CrossDocumentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.Importe);
    }
}
