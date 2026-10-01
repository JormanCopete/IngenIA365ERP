using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_ProductBarcodes</c> (feature 012, T206; data-model §1.8): el código es único en la cooperativa entre los vivos
/// (<c>UK_INV_ProductBarcodes_Barcode</c>; borrar uno lo libera para otro producto) y cada producto tiene a lo sumo un
/// principal. El empaque que identifica es una unidad alterna del producto (FK <c>Restrict</c>).
/// </summary>
public class ProductBarcodeConfiguration : IEntityTypeConfiguration<ProductBarcode>
{
    public void Configure(EntityTypeBuilder<ProductBarcode> builder)
    {
        builder.ComoEntidadDeInventario("INV_ProductBarcodes");

        builder.Property(e => e.Barcode).HasMaxLength(ProductBarcode.MaxLength).IsRequired();
        builder.Property(e => e.IsPrimary).IsRequired();

        builder.HasOne(e => e.ProductUnit).WithMany().HasForeignKey(e => e.ProductUnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Barcode).IsUnique().HasDatabaseName("UK_INV_ProductBarcodes_Barcode").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.ProductId, "UK_INV_ProductBarcodes_Primary")
            .IsUnique().HasFilter("[IsPrimary] = 1 AND [IsDeleted] = 0");
    }
}
