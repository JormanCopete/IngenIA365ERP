using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_PointsOfSale</c> (feature 012, I3, T583; FR-058; data-model §15): código único entre vivos; FK <c>Restrict</c> a sucursal,
/// canal y bodega por defecto. Declara también la FK de <c>INV_Documents.PointOfSaleId</c>. Entra en <c>VentasYPuntoDeVenta</c>.
/// </summary>
public class PointOfSaleConfiguration : IEntityTypeConfiguration<PointOfSale>
{
    public void Configure(EntityTypeBuilder<PointOfSale> builder)
    {
        builder.ComoEntidadDeInventario("INV_PointsOfSale");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_PointsOfSale_Code").HasFilter("[IsDeleted] = 0");
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(120);
        builder.Property(e => e.PosEnabled).HasDefaultValue(false);
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SalesChannel).WithMany().HasForeignKey(e => e.SalesChannelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DefaultWarehouse).WithMany().HasForeignKey(e => e.DefaultWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocument>().WithOne().HasForeignKey(d => d.PointOfSaleId).OnDelete(DeleteBehavior.Restrict);
    }
}
