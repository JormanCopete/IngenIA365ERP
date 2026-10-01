using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Warehousing;

/// <summary>
/// <c>INV_WarehouseLocations</c> (feature 012, T207; data-model §2.3): código único dentro de su bodega entre vivas y
/// exactamente una por defecto viva por bodega (<c>UK (WarehouseId)</c> filtrado). Declara también las FK de ubicación de
/// la línea del documento (<c>LocationId</c>, <c>ToLocationId</c>).
/// </summary>
public class WarehouseLocationConfiguration : IEntityTypeConfiguration<WarehouseLocation>
{
    public void Configure(EntityTypeBuilder<WarehouseLocation> builder)
    {
        builder.ComoEntidadDeInventario("INV_WarehouseLocations");

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.IsDefault).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasMany<InventoryDocumentLine>().WithOne().HasForeignKey(l => l.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocumentLine>().WithOne().HasForeignKey(l => l.ToLocationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.WarehouseId, e.Code })
            .IsUnique().HasDatabaseName("UK_INV_WarehouseLocations_Warehouse_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.WarehouseId, "UK_INV_WarehouseLocations_Default")
            .IsUnique().HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");
    }
}
