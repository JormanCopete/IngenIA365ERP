using IngenIA365ERP.Domain.Entities.Core.Taxes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Taxes;

/// <summary><c>COR_WithholdingConcepts</c> (feature 012, T161; data-model §17): código único entre vivos.</summary>
public class WithholdingConceptConfiguration : IEntityTypeConfiguration<WithholdingConcept>
{
    public void Configure(EntityTypeBuilder<WithholdingConcept> builder)
    {
        builder.ToTable("COR_WithholdingConcepts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_WithholdingConcepts_PublicId");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_COR_WithholdingConcepts_Code").HasFilter("[IsDeleted] = 0");
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.Notes).HasMaxLength(400);

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
