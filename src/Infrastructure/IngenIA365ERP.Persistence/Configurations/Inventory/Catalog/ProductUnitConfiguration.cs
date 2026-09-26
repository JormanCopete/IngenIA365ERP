using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_ProductUnits</c> (feature 012, T206; data-model §1.7): una fila viva por (producto, unidad), factor (18,6) y a lo
/// sumo una por defecto de compra y una de venta por producto (índices filtrados).
/// </summary>
public class ProductUnitConfiguration : IEntityTypeConfiguration<ProductUnit>
{
    public void Configure(EntityTypeBuilder<ProductUnit> builder)
    {
        builder.ComoEntidadDeInventario("INV_ProductUnits");

        builder.Property(e => e.Factor).Factor().IsRequired();
        builder.Property(e => e.UsedForPurchase).IsRequired();
        builder.Property(e => e.UsedForSale).IsRequired();
        builder.Property(e => e.IsDefaultPurchase).IsRequired();
        builder.Property(e => e.IsDefaultSale).IsRequired();

        builder.HasOne(e => e.Unit).WithMany().HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.UnitId })
            .IsUnique().HasDatabaseName("UK_INV_ProductUnits_Product_Unit").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.ProductId, "UK_INV_ProductUnits_DefaultPurchase")
            .IsUnique().HasFilter("[IsDefaultPurchase] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.ProductId, "UK_INV_ProductUnits_DefaultSale")
            .IsUnique().HasFilter("[IsDefaultSale] = 1 AND [IsDeleted] = 0");
    }
}
