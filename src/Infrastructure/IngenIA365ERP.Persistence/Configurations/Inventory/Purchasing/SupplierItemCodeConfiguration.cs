using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Purchasing;

/// <summary>
/// <c>INV_SupplierItemCodes</c> (compra directa desde el XML, 2026-11-14): el código del proveedor → el producto de la cooperativa, uno
/// vivo por proveedor y código (<c>UK (SupplierPersonId, ItemCode)</c> entre los vivos). (nuevo)
/// </summary>
public class SupplierItemCodeConfiguration : IEntityTypeConfiguration<SupplierItemCode>
{
    public void Configure(EntityTypeBuilder<SupplierItemCode> builder)
    {
        builder.ComoEntidadDeInventario("INV_SupplierItemCodes");

        builder.Property(e => e.ItemCode).HasMaxLength(SupplierItemCode.LargoDelCodigo).IsRequired();

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.SupplierPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductUnit>().WithMany().HasForeignKey(e => e.ProductUnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SupplierPersonId, e.ItemCode }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_INV_SupplierItemCodes_Supplier_ItemCode");
        builder.HasIndex(e => e.ProductId).HasDatabaseName("IX_INV_SupplierItemCodes_ProductId");
    }
}
