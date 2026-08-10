using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class JournalEntryItemConfiguration : IEntityTypeConfiguration<JournalEntryItem>
{
    public void Configure(EntityTypeBuilder<JournalEntryItem> builder)
    {
        builder.ToTable("ACC_JournalEntryItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_JournalEntryItems_PublicId");

        builder.Property(e => e.AccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.PersonTaxId).HasMaxLength(20);
        builder.Property(e => e.BranchCode).HasMaxLength(5);
        builder.Property(e => e.CostCenterCode).HasMaxLength(10);
        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.BaseAmount).HasPrecision(18, 2);
        builder.Property(e => e.DocumentType).HasMaxLength(5);
        builder.Property(e => e.DocumentNumber).HasMaxLength(20);
        builder.Property(e => e.Period).HasMaxLength(10);
        builder.Property(e => e.VoucherTypeCode).HasMaxLength(10);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
