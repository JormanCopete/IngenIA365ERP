using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Warehousing;

/// <summary>
/// <c>INV_Reservations</c> (feature 012, I6, T858; data-model §3.6 y §14): cantidades (18,4), estado como int, FK <c>Restrict</c> al
/// pedido, su línea, producto, bodega y al documento que la liberó. Índices <c>(ProductId, WarehouseId, Status)</c> para la
/// reconstrucción de <c>INV_StockBalances.Reserved</c> y <c>(ExpiresOn)</c> filtrado a las activas para la tarea que las vence. Es
/// estado, no hecho: se audita su diff. Nace con <c>ComercioAmpliado</c>.
/// </summary>
public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ComoEntidadDeInventario("INV_Reservations");

        builder.Property(e => e.QuantityBase).Cantidad().IsRequired();
        builder.Property(e => e.ConsumedQuantityBase).Cantidad().IsRequired();
        builder.Property(e => e.ExpiresOn).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ReleaseReason).HasMaxLength(Reservation.LargoDelMotivo);

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(e => e.DocumentLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.ReleasedByDocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.WarehouseId, e.Status }).HasDatabaseName("IX_INV_Reservations_Product_Warehouse_Status");
        builder.HasIndex(e => e.ExpiresOn).HasDatabaseName("IX_INV_Reservations_ExpiresOn").HasFilter("[Status] = 1");
        builder.HasIndex(e => e.DocumentId).HasDatabaseName("IX_INV_Reservations_DocumentId");
    }
}
