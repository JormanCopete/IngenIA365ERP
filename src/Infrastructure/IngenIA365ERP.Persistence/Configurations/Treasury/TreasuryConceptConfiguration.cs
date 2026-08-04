using IngenIA365ERP.Domain.Entities.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury;

public class TreasuryConceptConfiguration : IEntityTypeConfiguration<TreasuryConcept>
{
    public void Configure(EntityTypeBuilder<TreasuryConcept> builder)
    {
        builder.ToTable("TRS_Concepts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.ConceptCode).IsUnique();

        builder.Property(e => e.ConceptCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(20);
        builder.Property(e => e.ConceptType).HasMaxLength(5);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
