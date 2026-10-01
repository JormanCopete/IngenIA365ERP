using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Projections;

/// <summary>
/// <c>INV_StockBalances</c> (feature 012, T250; data-model §3.2): proyección de la existencia por (producto, bodega). El único
/// <c>UK_INV_StockBalances_Product_Warehouse</c> va <b>sin filtro</b>: la proyección nunca se da de baja y el cerrojo la crea
/// con <c>INSERT … ON CONFLICT DO NOTHING</c> / <c>WHERE NOT EXISTS</c> (<c>SqlDelCerrojo</c>). Cantidades (18,4).
/// </summary>
public class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ComoEntidadDeInventario("INV_StockBalances");

        builder.Property(e => e.Physical).Cantidad().IsRequired();
        builder.Property(e => e.Reserved).Cantidad().IsRequired();
        builder.Property(e => e.LastMovementDate);

        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.WarehouseId }).IsUnique().HasDatabaseName("UK_INV_StockBalances_Product_Warehouse");
        builder.HasIndex(e => e.WarehouseId).HasDatabaseName("IX_INV_StockBalances_WarehouseId");
    }
}
