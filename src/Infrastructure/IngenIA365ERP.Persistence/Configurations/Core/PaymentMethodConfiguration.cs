using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("COR_PaymentMethods");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_PaymentMethods_PublicId");

        builder.Property(e => e.VoucherTypeCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.DocumentNumber).IsRequired();
        builder.Property(e => e.Cash).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.Check).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.BankCode).HasMaxLength(10);
        builder.Property(e => e.CheckNumber).HasMaxLength(30);
        builder.Property(e => e.AccountNumber).HasMaxLength(30);
        builder.Property(e => e.DebitCard).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.DebitCardNumber).HasMaxLength(30);
        builder.Property(e => e.CreditCard).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.CreditCardNumber).HasMaxLength(30);
        builder.Property(e => e.OtherPayment).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.OtherPaymentNumber).HasMaxLength(30);
        builder.Property(e => e.TitleAmount).HasDefaultValue(0);
        builder.Property(e => e.TitleNumber).HasMaxLength(60);
        builder.Property(e => e.PaymentReason).HasMaxLength(4);
        builder.Property(e => e.CashReceiptCount).HasDefaultValue(0);
        builder.Property(e => e.CheckReceiptCount).HasDefaultValue(0);

        // Unique: VoucherTypeCode + DocumentNumber
        builder.HasIndex(e => new { e.VoucherTypeCode, e.DocumentNumber }).IsUnique().HasDatabaseName("UK_COR_PaymentMethods_Voucher_Doc");

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
