using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Documents;

/// <summary>
/// <c>INV_CountSnapshotLines</c> (feature 012, US11, T391; data-model §8.1): la foto de un conteo físico por producto, ubicación y
/// lote. Dos únicos filtrados <b>por lote</b> como en <c>INV_StockDetails</c>: <c>UK_INV_CountSnapshotLines_Location</c> con
/// <c>[LotId] IS NULL</c> y <c>UK_INV_CountSnapshotLines_Location_Lot</c> con <c>[LotId] IS NOT NULL</c>. Cantidades con
/// <c>PrecisionDeInventario.Cantidad</c>, el costo de la foto con <c>.CostoUnitario</c>. Sin migración propia: entra al par
/// <c>InventarioComercialNucleo</c> (T440). (nuevo)
/// </summary>
public class CountSnapshotLineConfiguration : IEntityTypeConfiguration<CountSnapshotLine>
{
    public const string UnicoSinLote = "UK_INV_CountSnapshotLines_Location";
    public const string UnicoConLote = "UK_INV_CountSnapshotLines_Location_Lot";

    public void Configure(EntityTypeBuilder<CountSnapshotLine> builder)
    {
        builder.ComoEntidadDeInventario("INV_CountSnapshotLines");

        builder.Property(e => e.TheoreticalQuantity).Cantidad().IsRequired();
        builder.Property(e => e.SnapshotUnitCost).CostoUnitario().IsRequired();
        builder.Property(e => e.AddedDuringCapture).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.MovementsAfterSnapshot).Cantidad();
        builder.Property(e => e.CountedQuantity).Cantidad();
        builder.Property(e => e.Difference).Cantidad();
        builder.Property(e => e.RecountRequired).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.LastRound).HasDefaultValue((byte)0).IsRequired();

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseLocation>().WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Captures).WithOne(c => c.SnapshotLine).HasForeignKey(c => c.SnapshotLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentId, e.ProductId, e.LocationId }).IsUnique()
            .HasDatabaseName(UnicoSinLote).HasFilter("[LotId] IS NULL");
        builder.HasIndex(e => new { e.DocumentId, e.ProductId, e.LocationId, e.LotId }).IsUnique()
            .HasDatabaseName(UnicoConLote).HasFilter("[LotId] IS NOT NULL");
        builder.HasIndex(e => new { e.ProductId, e.DocumentId }).HasDatabaseName("IX_INV_CountSnapshotLines_Product_Document");
    }
}
