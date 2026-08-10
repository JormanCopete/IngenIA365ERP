using IngenIA365ERP.Domain.Entities.Debit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Debit;

public class DebitDailyParameterConfiguration : IEntityTypeConfiguration<DebitDailyParameter>
{
    public void Configure(EntityTypeBuilder<DebitDailyParameter> builder)
    {
        builder.ToTable("DEB_DailyParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.ParameterCode).IsUnique();

        builder.Property(e => e.BankId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.BatchVoucherCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.OnlineVoucherCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(50).IsRequired();
        builder.Property(e => e.LastCardNumber).HasMaxLength(25);
        builder.Property(e => e.ClosingVoucherCode).HasMaxLength(5);
        builder.Property(e => e.PosClosingVoucherCode).HasMaxLength(5);
        builder.Property(e => e.ClosingFlag).HasMaxLength(2);
        builder.Property(e => e.NetworkCommission).HasPrecision(18, 2);
        builder.Property(e => e.OtherNetworkCommission).HasPrecision(18, 2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
