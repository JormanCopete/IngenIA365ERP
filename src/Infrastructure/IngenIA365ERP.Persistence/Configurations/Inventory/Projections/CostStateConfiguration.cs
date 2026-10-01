using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Projections;

/// <summary>
/// <c>INV_CostStates</c> (feature 012, T250; data-model §3.4): proyección de costo por (producto, ámbito). <c>ScopeWarehouseId</c>
/// sin FK (0 = cooperativa). Único <c>UK_INV_CostStates_Product_Scope</c> sin filtro (el cerrojo crea la fila que falta).
/// Cantidad (18,4), valor (18,2), promedio y último costo (18,6).
/// </summary>
public class CostStateConfiguration : IEntityTypeConfiguration<CostState>
{
    public void Configure(EntityTypeBuilder<CostState> builder)
    {
        builder.ComoEntidadDeInventario("INV_CostStates");

        builder.Property(e => e.ScopeWarehouseId).IsRequired();
        builder.Property(e => e.Method).HasConversion<int>().IsRequired();
        builder.Property(e => e.Quantity).Cantidad().IsRequired();
        builder.Property(e => e.Value).Monto().IsRequired();
        builder.Property(e => e.AverageCost).CostoUnitario().IsRequired();
        builder.Property(e => e.LastUnitCost).CostoUnitario().IsRequired();

        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.ScopeWarehouseId }).IsUnique().HasDatabaseName("UK_INV_CostStates_Product_Scope");
    }
}
