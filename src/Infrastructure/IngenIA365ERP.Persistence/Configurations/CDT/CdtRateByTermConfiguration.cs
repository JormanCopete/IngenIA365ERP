using IngenIA365ERP.Domain.Entities.CDT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.CDT;

public class CdtRateByTermConfiguration : IEntityTypeConfiguration<CdtRateByTerm>
{
    public void Configure(EntityTypeBuilder<CdtRateByTerm> builder)
    {
        builder.ToTable("CDT_RatesByTerm");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.CreditLineId, e.AmountRangeStart, e.AmountRangeEnd, e.TermStart, e.TermEnd }).IsUnique();

        builder.Property(e => e.AmountRangeStart).HasPrecision(18, 2);
        builder.Property(e => e.AmountRangeEnd).HasPrecision(18, 2);
        builder.Property(e => e.InterestRate).HasPrecision(10, 5);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
