using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("ACC_JournalEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.VoucherTypeCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.PeriodCode).HasMaxLength(10);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.AuxiliaryDocument).HasMaxLength(50);
        builder.Property(e => e.InvoiceNumber).HasMaxLength(50);
        builder.Property(e => e.UserName).HasMaxLength(100);
        builder.Property(e => e.DocumentType).HasMaxLength(10);
        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);
        builder.Property(e => e.BaseAmount).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.VoucherTypeCode, e.DocumentNumber });
        builder.HasIndex(e => e.TransactionDate);
        builder.HasIndex(e => e.PersonId);

        builder.HasOne(e => e.Account).WithMany(a => a.JournalEntries).HasForeignKey(e => e.AccountId);
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
