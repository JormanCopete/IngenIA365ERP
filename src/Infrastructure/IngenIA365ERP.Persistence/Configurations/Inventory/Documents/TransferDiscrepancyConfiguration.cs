using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Documents;

/// <summary>
/// <c>INV_TransferDiscrepancies</c> (feature 012, US10, T366; data-model §7.1): los faltantes y sobrantes de un traslado, una fila
/// por recepción, línea de despacho y tipo (<c>UK (ReceiptDocumentId, DispatchLineId, Kind)</c> entre los vivos) y el índice de
/// las pendientes (<c>IX (ResolvedAt)</c> filtrado a nulos) que usan la bandeja y el cierre del mes. Cantidades con
/// <c>PrecisionDeInventario.Cantidad</c>, costo con <c>.CostoUnitario</c>. Sin migración propia: entra al par
/// <c>InventarioComercialNucleo</c> (T440).
/// </summary>
public class TransferDiscrepancyConfiguration : IEntityTypeConfiguration<TransferDiscrepancy>
{
    public void Configure(EntityTypeBuilder<TransferDiscrepancy> builder)
    {
        builder.ComoEntidadDeInventario("INV_TransferDiscrepancies");

        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.QuantityBase).Cantidad().IsRequired();
        builder.Property(e => e.UnitCost).CostoUnitario();
        builder.Property(e => e.ResolvedQuantityBase).Cantidad().HasDefaultValue(0m).IsRequired();
        builder.Property(e => e.ResolutionQuantityBase).Cantidad();
        builder.Property(e => e.ResolutionReason).HasMaxLength(TransferDiscrepancy.LargoDelMotivo);

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DispatchDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.ReceiptDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.ResolutionDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(e => e.DispatchLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdjustmentCause>().WithMany().HasForeignKey(e => e.AdjustmentCauseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ReceiptDocumentId, e.DispatchLineId, e.Kind }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_INV_TransferDiscrepancies_Receipt_DispatchLine_Kind");
        builder.HasIndex(e => e.ResolvedAt).HasFilter("[ResolvedAt] IS NULL")
            .HasDatabaseName("IX_INV_TransferDiscrepancies_ResolvedAt");
        builder.HasIndex(e => e.DispatchDocumentId).HasDatabaseName("IX_INV_TransferDiscrepancies_DispatchDocumentId");
    }
}
