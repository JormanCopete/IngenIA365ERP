using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class PaymentMethodCheckConfiguration : IEntityTypeConfiguration<PaymentMethodCheck>
{
    public void Configure(EntityTypeBuilder<PaymentMethodCheck> builder)
    {
        builder.ToTable("COR_PaymentMethodChecks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_PaymentMethodChecks_PublicId");

        builder.Property(e => e.VoucherTypeCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.DocumentNumber).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.CheckNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.BankCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.AccountNumber).HasMaxLength(30);
        builder.Property(e => e.LegacyUser).HasMaxLength(30);

        // Unique: VoucherTypeCode + DocumentNumber + CheckNumber + BankCode
        builder.HasIndex(e => new { e.VoucherTypeCode, e.DocumentNumber, e.CheckNumber, e.BankCode }).IsUnique().HasDatabaseName("UK_COR_PaymentMethodChecks_Voucher_Doc_Check_Bank");

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
