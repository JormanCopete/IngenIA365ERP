using IngenIA365ERP.Domain.Entities.Debit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Debit;

public class DebitTransactionConfiguration : IEntityTypeConfiguration<DebitTransaction>
{
    public void Configure(EntityTypeBuilder<DebitTransaction> builder)
    {
        builder.ToTable("DEB_Transactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.SequenceCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CardNumber).HasMaxLength(25).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.TransactionType).HasMaxLength(5);
        builder.Property(e => e.CausalCode).HasMaxLength(5);
        builder.Property(e => e.Status).HasMaxLength(2);
        builder.Property(e => e.SourceSystem).HasMaxLength(20);
        builder.Property(e => e.TransactionTime).HasMaxLength(10);
        builder.Property(e => e.NetworkCode).HasMaxLength(10);
        builder.Property(e => e.VatAmount).HasPrecision(18, 2);
        builder.Property(e => e.VatBase).HasPrecision(18, 2);
        builder.Property(e => e.CommissionAmount).HasPrecision(18, 2);
        builder.Property(e => e.ErrorCode).HasMaxLength(5);
        builder.Property(e => e.MerchantCode).HasMaxLength(20);
        builder.Property(e => e.AuthorizationCode).HasMaxLength(20);

        builder.HasIndex(e => e.CardId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
