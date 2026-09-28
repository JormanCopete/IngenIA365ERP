using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Purchasing;

/// <summary>
/// <c>INV_PurchaseMatchLines</c> (feature 012, US13, T783; data-model §9.6): el cruce a tres vías por línea de factura del
/// proveedor. Cantidades con <c>PrecisionDeInventario.Cantidad</c>, precios con <c>.PrecioUnitario</c>, la diferencia en pesos con
/// <c>.Monto</c> y su tarifa con <c>.Tarifa</c>; <c>IX (InvoiceDocumentId)</c>, FK <c>Restrict</c> y el filtro de borrado lógico
/// (las filas de un intento anterior se dan de baja al recalcular). Sin migración propia: entra al par
/// <c>ComprasYCosteoAvanzado</c> (T835).
/// </summary>
public class PurchaseMatchLineConfiguration : IEntityTypeConfiguration<PurchaseMatchLine>
{
    public void Configure(EntityTypeBuilder<PurchaseMatchLine> builder)
    {
        builder.ComoEntidadDeInventario("INV_PurchaseMatchLines");

        builder.Property(e => e.OrderedQuantity).Cantidad().IsRequired();
        builder.Property(e => e.ReceivedNotInvoicedQuantity).Cantidad().IsRequired();
        builder.Property(e => e.InvoicedQuantity).Cantidad().IsRequired();
        builder.Property(e => e.OrderedUnitPrice).PrecioUnitario();
        builder.Property(e => e.ReceivedUnitCost).PrecioUnitario();
        builder.Property(e => e.InvoicedUnitPrice).PrecioUnitario();
        builder.Property(e => e.QuantityDifference).Cantidad().IsRequired();
        builder.Property(e => e.PriceDifferenceAmount).Monto().IsRequired();
        builder.Property(e => e.PriceDifferenceRate).Tarifa().IsRequired();
        builder.Property(e => e.ExceedsTolerance).IsRequired();
        builder.Property(e => e.ToleranceJson).HasMaxLength(PurchaseMatchLine.LargoDeToleranceJson).IsRequired();
        builder.Property(e => e.ApprovalRequestPublicId);
        builder.Property(e => e.Status);
        builder.Property(e => e.Reasons).HasMaxLength(PurchaseMatchLine.LargoDeReasons);

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.InvoiceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(e => e.InvoiceLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(e => e.OrderLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.InvoiceDocumentId).HasDatabaseName("IX_INV_PurchaseMatchLines_InvoiceDocumentId");
    }
}
