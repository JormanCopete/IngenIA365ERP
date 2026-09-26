using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Projections;

/// <summary>
/// <c>INV_StockDetails</c> (feature 012, T250; data-model §3.3): proyección por (producto, bodega, ubicación, lote). Dos únicos
/// filtrados <b>por lote</b>, nunca por borrado (PostgreSQL trata los nulos como distintos): <c>UK_INV_StockDetails_Location</c>
/// con <c>[LotId] IS NULL</c> y <c>UK_INV_StockDetails_Location_Lot</c> con <c>[LotId] IS NOT NULL</c>. Son los que choca el
/// <c>ON CONFLICT DO NOTHING</c> del cerrojo. Lote sin FK hasta I6. Cantidad (18,4).
/// </summary>
public class StockDetailConfiguration : IEntityTypeConfiguration<StockDetail>
{
    public const string UnicoSinLote = "UK_INV_StockDetails_Location";
    public const string UnicoConLote = "UK_INV_StockDetails_Location_Lot";

    public void Configure(EntityTypeBuilder<StockDetail> builder)
    {
        builder.ComoEntidadDeInventario("INV_StockDetails");

        builder.Property(e => e.Quantity).Cantidad().IsRequired();

        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseLocation>().WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.WarehouseId, e.LocationId }).IsUnique()
            .HasDatabaseName(UnicoSinLote).HasFilter("[LotId] IS NULL");
        builder.HasIndex(e => new { e.ProductId, e.WarehouseId, e.LocationId, e.LotId }).IsUnique()
            .HasDatabaseName(UnicoConLote).HasFilter("[LotId] IS NOT NULL");
        builder.HasIndex(e => e.LocationId).HasDatabaseName("IX_INV_StockDetails_LocationId");
    }
}
