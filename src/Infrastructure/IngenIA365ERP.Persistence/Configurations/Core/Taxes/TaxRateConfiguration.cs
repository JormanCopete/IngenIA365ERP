using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Taxes;

/// <summary>
/// <c>COR_TaxRates</c> (feature 012, T161; data-model §17, T19): tarifa como fracción (9,6) con
/// <see cref="PrecisionDeInventario"/>, valor por unidad y base en pesos (18,2), base en UVT (18,4). Único
/// <c>(Code, ValidFrom)</c> entre vivos; que dos vigencias del mismo código no se crucen lo cuida el comando
/// (<c>Core.TaxRate.Overlaps</c>).
/// </summary>
public class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("COR_TaxRates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_TaxRates_PublicId");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => new { e.Code, e.ValidFrom }).IsUnique().HasDatabaseName("UK_COR_TaxRates_Code_ValidFrom").HasFilter("[IsDeleted] = 0");
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Rate).Tarifa();
        builder.Property(e => e.AmountPerUnit).Monto();
        builder.Property(e => e.MunicipalityDaneCode).HasMaxLength(5);
        builder.Property(e => e.ActivityCode).HasMaxLength(6);
        builder.Property(e => e.MinimumBaseUvt).Cantidad();
        builder.Property(e => e.MinimumBasePesos).Monto();
        builder.Property(e => e.SubjectPersonType).HasMaxLength(2);
        builder.Property(e => e.AppliesTo).HasConversion<int>().IsRequired();
        builder.Property(e => e.Priority).HasDefaultValue((short)0);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.LegalSource).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ReviewPending).HasDefaultValue(false);
        builder.Property(e => e.Notes).HasMaxLength(400);

        builder.HasOne(e => e.TaxDefinition).WithMany(d => d.Rates).HasForeignKey(e => e.TaxDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WithholdingConcept).WithMany().HasForeignKey(e => e.WithholdingConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.TaxDefinitionId).HasDatabaseName("IX_COR_TaxRates_TaxDefinitionId");
        builder.HasIndex(e => e.WithholdingConceptId).HasDatabaseName("IX_COR_TaxRates_WithholdingConceptId");
        builder.HasIndex(e => e.MunicipalityDaneCode).HasDatabaseName("IX_COR_TaxRates_MunicipalityDaneCode");

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
