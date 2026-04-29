using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class ContributionReductionParamConfiguration : IEntityTypeConfiguration<ContributionReductionParam>
{
    public void Configure(EntityTypeBuilder<ContributionReductionParam> builder)
    {
        builder.ToTable("LND_ContributionReductionParams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_ContributionReductionParams_PublicId");

        builder.Property(e => e.SignatoryName).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Position).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ReductionVoucher).HasMaxLength(5).IsRequired();
        builder.Property(e => e.ExcessAmount).HasPrecision(15, 0);
        builder.HasIndex(e => e.CutoffPeriod).IsUnique().HasDatabaseName("UK_LND_ContribRedParams_Period");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
