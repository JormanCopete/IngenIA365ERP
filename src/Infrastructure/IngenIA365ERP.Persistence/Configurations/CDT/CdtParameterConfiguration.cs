using IngenIA365ERP.Domain.Entities.CDT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.CDT;

public class CdtParameterConfiguration : IEntityTypeConfiguration<CdtParameter>
{
    public void Configure(EntityTypeBuilder<CdtParameter> builder)
    {
        builder.ToTable("CDT_Parameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.CreditLineId).IsUnique();

        builder.Property(e => e.Description).HasMaxLength(100);
        builder.Property(e => e.MinimumRate).HasPrecision(5, 2);
        builder.Property(e => e.AnnualRate).HasPrecision(12, 9);
        builder.Property(e => e.WithholdingRate).HasPrecision(4, 2);
        builder.Property(e => e.MinWithholdingAmount).HasPrecision(18, 2);
        builder.Property(e => e.MonthlyIncrement).HasPrecision(4, 2);
        builder.Property(e => e.InterestRate).HasPrecision(10, 5);
        builder.Property(e => e.MinAmount).HasPrecision(18, 2);
        builder.Property(e => e.MaxAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestType).HasMaxLength(2).HasDefaultValue("S");
        builder.Property(e => e.TreasuryAccount).HasMaxLength(15);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
