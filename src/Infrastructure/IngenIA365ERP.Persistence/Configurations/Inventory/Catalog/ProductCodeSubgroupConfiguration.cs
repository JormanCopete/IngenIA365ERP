using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_ProductCodeSubgroups</c> (codificación automática de productos, 2026-11-12): una marca tiene un solo número por categoría
/// y un número es de una sola marca en la categoría, entre vivas. (nuevo)
/// </summary>
public class ProductCodeSubgroupConfiguration : IEntityTypeConfiguration<ProductCodeSubgroup>
{
    public void Configure(EntityTypeBuilder<ProductCodeSubgroup> builder)
    {
        builder.ComoEntidadDeInventario("INV_ProductCodeSubgroups");

        builder.Property(e => e.Segment).HasMaxLength(4).IsRequired();

        builder.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Brand).WithMany().HasForeignKey(e => e.BrandId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CategoryId, e.BrandId }).IsUnique()
            .HasDatabaseName("UK_INV_ProductCodeSubgroups_Category_Brand").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.CategoryId, e.Segment }).IsUnique()
            .HasDatabaseName("UK_INV_ProductCodeSubgroups_Category_Segment").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.BrandId).HasDatabaseName("IX_INV_ProductCodeSubgroups_BrandId");
    }
}
