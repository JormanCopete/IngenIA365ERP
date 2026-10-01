using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_ProductTaxes</c> (feature 012, T206; data-model §1.9): un impuesto vivo por (producto, definición), FK
/// <c>Restrict</c> a <c>COR_TaxDefinitions</c>, la tarifa por su código estable y las unidades gravables como factor.
/// </summary>
public class ProductTaxConfiguration : IEntityTypeConfiguration<ProductTax>
{
    public void Configure(EntityTypeBuilder<ProductTax> builder)
    {
        builder.ComoEntidadDeInventario("INV_ProductTaxes");

        builder.Property(e => e.TaxRateCode).HasMaxLength(10);
        builder.Property(e => e.AppliesTo).HasConversion<int>().IsRequired();
        builder.Property(e => e.TaxableUnitsPerBaseUnit).Factor();

        builder.HasOne(e => e.TaxDefinition).WithMany().HasForeignKey(e => e.TaxDefinitionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.TaxDefinitionId })
            .IsUnique().HasDatabaseName("UK_INV_ProductTaxes_Product_Tax").HasFilter("[IsDeleted] = 0");
    }
}
