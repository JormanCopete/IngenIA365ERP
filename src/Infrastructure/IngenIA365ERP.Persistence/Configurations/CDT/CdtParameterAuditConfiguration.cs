using IngenIA365ERP.Domain.Entities.CDT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.CDT;

public class CdtParameterAuditConfiguration : IEntityTypeConfiguration<CdtParameterAudit>
{
    public void Configure(EntityTypeBuilder<CdtParameterAudit> builder)
    {
        builder.ToTable("CDT_ParameterAudit");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Action).HasMaxLength(1).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(50);
        builder.Property(e => e.DescriptionOld).HasMaxLength(100);
        builder.Property(e => e.DescriptionNew).HasMaxLength(100);
        builder.Property(e => e.MinRateOld).HasPrecision(5, 2);
        builder.Property(e => e.MinRateNew).HasPrecision(5, 2);
        builder.Property(e => e.AnnualRateOld).HasPrecision(12, 9);
        builder.Property(e => e.AnnualRateNew).HasPrecision(12, 9);
        builder.Property(e => e.WithholdingRateOld).HasPrecision(4, 2);
        builder.Property(e => e.WithholdingRateNew).HasPrecision(4, 2);
        builder.Property(e => e.MinWithholdingAmtOld).HasPrecision(18, 2);
        builder.Property(e => e.MinWithholdingAmtNew).HasPrecision(18, 2);
        builder.Property(e => e.MonthlyIncrementOld).HasPrecision(4, 2);
        builder.Property(e => e.MonthlyIncrementNew).HasPrecision(4, 2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
