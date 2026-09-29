using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_VariantAttributes</c>, <c>INV_VariantAttributeValues</c> e <c>INV_ProductVariantValues</c> (feature 012, I6, T857;
/// data-model §1.11): código de 10 único entre vivos en el atributo, <c>(VariantAttributeId, Code)</c> en el valor y
/// <c>(ProductId, VariantAttributeId)</c> en la variante (un valor por atributo); FK <c>Restrict</c>. Nacen con <c>ComercioAmpliado</c>.
/// </summary>
public class VariantAttributeConfiguration : IEntityTypeConfiguration<VariantAttribute>
{
    public void Configure(EntityTypeBuilder<VariantAttribute> builder)
    {
        builder.ComoEntidadDeInventario("INV_VariantAttributes");

        builder.Property(e => e.Code).HasMaxLength(VariantAttribute.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(VariantAttribute.LargoDelNombre).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true).IsRequired();

        builder.HasMany(e => e.Values).WithOne(v => v.VariantAttribute).HasForeignKey(v => v.VariantAttributeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_VariantAttributes_Code").HasFilter("[IsDeleted] = 0");
    }
}

/// <inheritdoc cref="VariantAttributeConfiguration"/>
public class VariantAttributeValueConfiguration : IEntityTypeConfiguration<VariantAttributeValue>
{
    public void Configure(EntityTypeBuilder<VariantAttributeValue> builder)
    {
        builder.ComoEntidadDeInventario("INV_VariantAttributeValues");

        builder.Property(e => e.Code).HasMaxLength(VariantAttribute.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(VariantAttribute.LargoDelNombre).IsRequired();
        builder.Property(e => e.SortOrder).HasDefaultValue(0).IsRequired();

        builder.HasIndex(e => new { e.VariantAttributeId, e.Code }).IsUnique()
            .HasDatabaseName("UK_INV_VariantAttributeValues_Attribute_Code").HasFilter("[IsDeleted] = 0");
    }
}

/// <inheritdoc cref="VariantAttributeConfiguration"/>
public class ProductVariantValueConfiguration : IEntityTypeConfiguration<ProductVariantValue>
{
    public void Configure(EntityTypeBuilder<ProductVariantValue> builder)
    {
        builder.ComoEntidadDeInventario("INV_ProductVariantValues");

        builder.HasOne(e => e.VariantAttribute).WithMany().HasForeignKey(e => e.VariantAttributeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.VariantAttributeValue).WithMany().HasForeignKey(e => e.VariantAttributeValueId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.VariantAttributeId }).IsUnique()
            .HasDatabaseName("UK_INV_ProductVariantValues_Product_Attribute").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.VariantAttributeValueId).HasDatabaseName("IX_INV_ProductVariantValues_ValueId");
    }
}
