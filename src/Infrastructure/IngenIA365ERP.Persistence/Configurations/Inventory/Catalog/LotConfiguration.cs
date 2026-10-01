using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_Lots</c> (feature 012, I6, T857; data-model §1.11): único <c>(ProductId, Code)</c> entre vivos; vencimiento y fabricación
/// como <c>date</c>; FK <c>Restrict</c> al producto; índice por vencimiento para proponer el que vence primero y la alerta
/// <c>Inventario.ProximoAVencer</c>. Nace con <c>ComercioAmpliado</c>.
/// </summary>
public class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ComoEntidadDeInventario("INV_Lots");

        builder.Property(e => e.Code).HasMaxLength(Lot.LargoDelCodigo).IsRequired();

        builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.Code }).IsUnique().HasDatabaseName("UK_INV_Lots_Product_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.ProductId, e.ExpiryDate }).HasDatabaseName("IX_INV_Lots_Product_ExpiryDate");
    }
}

/// <summary>
/// <c>INV_Serials</c> (feature 012, I6, T857; data-model §1.11): único <c>(ProductId, SerialNumber)</c> entre vivos; FK
/// <c>Restrict</c> al producto, al lote y a la bodega y ubicación de su proyección (<c>InStock*</c>). La entidad es
/// <c>[SinDiffDeAuditoria]</c>, como las demás proyecciones. Nace con <c>ComercioAmpliado</c>.
/// </summary>
public class SerialConfiguration : IEntityTypeConfiguration<Serial>
{
    public void Configure(EntityTypeBuilder<Serial> builder)
    {
        builder.ComoEntidadDeInventario("INV_Serials");

        builder.Property(e => e.SerialNumber).HasMaxLength(Serial.LargoDelNumero).IsRequired();

        builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Lot).WithMany().HasForeignKey(e => e.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.InStockWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseLocation>().WithMany().HasForeignKey(e => e.InStockLocationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.SerialNumber }).IsUnique()
            .HasDatabaseName("UK_INV_Serials_Product_SerialNumber").HasFilter("[IsDeleted] = 0");
    }
}
