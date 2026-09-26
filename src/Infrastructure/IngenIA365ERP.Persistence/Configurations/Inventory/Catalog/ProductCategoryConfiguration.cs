using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_ProductCategories</c> (feature 012, T206; data-model §1.2): árbol con FK a sí misma (<c>Restrict</c>), nivel como
/// <c>tinyint</c> y la ruta materializada de Ids <b>(nuevo)</b> con su índice (el conteo y los informes por categoría la
/// recorren con un prefijo).
/// </summary>
public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ComoEntidadDeInventario("INV_ProductCategories");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Level).IsRequired();
        builder.Property(e => e.Path).HasMaxLength(100).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasOne(e => e.Parent).WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_ProductCategories_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.ParentId).HasDatabaseName("IX_INV_ProductCategories_ParentId");
        builder.HasIndex(e => e.Path).HasDatabaseName("IX_INV_ProductCategories_Path");
    }
}
