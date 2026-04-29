using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class ScoringRangeConfiguration : IEntityTypeConfiguration<ScoringRange>
{
    public void Configure(EntityTypeBuilder<ScoringRange> builder)
    {
        builder.ToTable("LND_ScoringRanges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_ScoringRanges_PublicId");

        builder.Property(e => e.CriterionCode).HasMaxLength(2).IsRequired();
        builder.Property(e => e.SubItemCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.RangeStart).HasMaxLength(40).IsRequired();
        builder.Property(e => e.RangeEnd).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Equality).HasMaxLength(3).IsRequired();


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
