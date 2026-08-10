using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class SavingsParameterConfiguration : IEntityTypeConfiguration<SavingsParameter>
{
    public void Configure(EntityTypeBuilder<SavingsParameter> builder)
    {
        builder.ToTable("LND_SavingsParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_SavingsParameters_PublicId");

        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(25).IsRequired();
        builder.Property(e => e.InterestPaymentPeriod).HasMaxLength(2).IsRequired();
        builder.Property(e => e.LiquidationForm).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PaymentForm).HasMaxLength(2).IsRequired();
        builder.Property(e => e.TreasuryAccount).HasMaxLength(15).IsRequired();
        builder.Property(e => e.MinInterestBalance).HasPrecision(18, 2);
        builder.Property(e => e.InterestPaymentRate).HasPrecision(9, 5);
        builder.Property(e => e.WithholdingRate).HasPrecision(9, 5);
        builder.Property(e => e.TaxRate).HasPrecision(10, 5);
        builder.Property(e => e.MaxCashAmount).HasPrecision(18, 2);
        builder.HasIndex(e => e.SavingsLineId).IsUnique().HasDatabaseName("UK_LND_SavingsParameters_LineId");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
