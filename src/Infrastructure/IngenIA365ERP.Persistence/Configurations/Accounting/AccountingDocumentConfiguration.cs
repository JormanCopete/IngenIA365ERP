using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class AccountingDocumentConfiguration : IEntityTypeConfiguration<AccountingDocument>
{
    public void Configure(EntityTypeBuilder<AccountingDocument> builder)
    {
        builder.ToTable("ACC_Documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_Documents_PublicId");

        builder.Property(e => e.LegacyCompronte).HasMaxLength(5);
        builder.Property(e => e.VoucherTypeCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Detail).HasMaxLength(200);
        builder.Property(e => e.TotalDebit).HasPrecision(18, 2);
        builder.Property(e => e.TotalCredit).HasPrecision(18, 2);
        builder.Property(e => e.IsClosed).HasDefaultValue(false);
        builder.Property(e => e.IsVoided).HasDefaultValue(false);
        builder.Property(e => e.CheckNumber).HasMaxLength(10);
        builder.Property(e => e.ModuleCode).HasMaxLength(5);
        builder.Property(e => e.PaymentMethod).HasMaxLength(2);

        builder.HasIndex(e => new { e.VoucherTypeCode, e.DocumentNumber }).IsUnique().HasDatabaseName("UK_ACC_Documents_VoucherDoc");
        builder.HasIndex(e => e.VoucherTypeCode).HasDatabaseName("IX_ACC_Documents_VoucherTypeCode");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
