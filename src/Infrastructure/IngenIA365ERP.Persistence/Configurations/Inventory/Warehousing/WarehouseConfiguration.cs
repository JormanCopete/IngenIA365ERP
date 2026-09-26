using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Warehousing;

/// <summary>
/// <c>INV_Warehouses</c> (feature 012, T207; data-model §2.2): código único entre vivos (inmutable), <c>IX (BranchId)</c> y
/// <c>UK_INV_Warehouses_Branch_Transit</c>, una sola bodega de tránsito viva por sucursal. FK <c>Restrict</c> a
/// <c>COR_Branches</c>, al tipo y a <c>SEC_Users</c> (quien activó). Declara también las FK de bodega que la fase 3 dejó
/// para esta historia: bodega permitida de un tipo (<c>INV_DocumentTypeWarehouses</c>) y las tres bodegas del documento
/// (<c>WarehouseId</c>, <c>DestinationWarehouseId</c>, <c>TransitWarehouseId</c>).
/// </summary>
public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ComoEntidadDeInventario("INV_Warehouses");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Behavior).HasConversion<int>().IsRequired();
        builder.Property(e => e.ActivationStatus).HasConversion<int>().IsRequired();
        builder.Property(e => e.Address).HasMaxLength(200);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WarehouseType).WithMany().HasForeignKey(e => e.WarehouseTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActivatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Locations).WithOne(l => l.Warehouse).HasForeignKey(l => l.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<DocumentTypeWarehouse>().WithOne().HasForeignKey(t => t.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocument>().WithOne().HasForeignKey(d => d.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocument>().WithOne().HasForeignKey(d => d.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocument>().WithOne().HasForeignKey(d => d.TransitWarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_Warehouses_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.BranchId, "IX_INV_Warehouses_BranchId");
        builder.HasIndex(e => e.BranchId, "UK_INV_Warehouses_Branch_Transit")
            .IsUnique().HasFilter("[Behavior] = 2 AND [IsDeleted] = 0");
    }
}
