using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Purchasing;

/// <summary>
/// <c>INV_LandedCostAllocations</c> (feature 012, US13, T784; data-model §9.7): la porción de un documento de costos adicionales
/// por línea de recepción, una por documento y línea (<c>UK (DocumentId, ReceiptLineId)</c> entre las vivas). <c>Basis</c> va en
/// decimal(18,6) con <c>PrecisionDeInventario.Factor</c> (pesos, cantidad, kg o litros según el método), los montos con
/// <c>.Monto</c> y <c>ExistingRatio</c> con <c>.Tarifa</c>; FK <c>Restrict</c>. Sin migración propia: entra al par
/// <c>ComprasYCosteoAvanzado</c> (T835).
/// </summary>
public class LandedCostAllocationConfiguration : IEntityTypeConfiguration<LandedCostAllocation>
{
    public void Configure(EntityTypeBuilder<LandedCostAllocation> builder)
    {
        builder.ComoEntidadDeInventario("INV_LandedCostAllocations");

        builder.Property(e => e.AllocationMethod).IsRequired();
        builder.Property(e => e.Basis).Factor().IsRequired();
        builder.Property(e => e.AllocatedAmount).Monto().IsRequired();
        builder.Property(e => e.RoundingResidue).Monto().HasDefaultValue(0m).IsRequired();
        builder.Property(e => e.ExistingRatio).Tarifa().IsRequired();
        builder.Property(e => e.ExistingAmount).Monto().IsRequired();
        builder.Property(e => e.SoldAmount).Monto().IsRequired();

        builder.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.ReceiptDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(e => e.ReceiptLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentId, e.ReceiptLineId }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_INV_LandedCostAllocations_Document_ReceiptLine");
        builder.HasIndex(e => e.ReceiptDocumentId).HasDatabaseName("IX_INV_LandedCostAllocations_ReceiptDocumentId");
    }
}
