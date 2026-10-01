using IngenIA365ERP.Domain.Entities.Core.Taxes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Taxes;

/// <summary>
/// <c>COR_TaxDefinitions</c> (feature 012, T161; data-model §17): código único entre vivos; <c>TaxedOnDefinitionId</c>
/// es una FK a la misma tabla con <c>Restrict</c> (ReteIVA sobre el IVA).
/// </summary>
public class TaxDefinitionConfiguration : IEntityTypeConfiguration<TaxDefinition>
{
    public void Configure(EntityTypeBuilder<TaxDefinition> builder)
    {
        builder.ToTable("COR_TaxDefinitions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_TaxDefinitions_PublicId");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_COR_TaxDefinitions_Code").HasFilter("[IsDeleted] = 0");
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.CalculationForm).HasConversion<int>().IsRequired();
        builder.Property(e => e.IsWithholding).HasDefaultValue(false);
        builder.Property(e => e.DianTaxCode).HasMaxLength(4);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.Notes).HasMaxLength(400);

        builder.HasOne(e => e.TaxedOnDefinition).WithMany().HasForeignKey(e => e.TaxedOnDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.TaxedOnDefinitionId).HasDatabaseName("IX_COR_TaxDefinitions_TaxedOnDefinitionId");

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
