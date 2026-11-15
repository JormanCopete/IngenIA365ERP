using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pricing;

/// <summary>
/// <c>INV_PriceListItemHistory</c> (2026-11-15): la historia de cada precio de una lista, por lista y producto, en pesos (18,2).
/// </summary>
public class PriceListItemHistoryConfiguration : IEntityTypeConfiguration<PriceListItemHistory>
{
    public void Configure(EntityTypeBuilder<PriceListItemHistory> builder)
    {
        builder.ComoEntidadDeInventario("INV_PriceListItemHistory");
        builder.Property(e => e.PreviousPrice).Monto();
        builder.Property(e => e.NewPrice).Monto();
        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.HasOne(e => e.PriceList).WithMany().HasForeignKey(e => e.PriceListId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(e => e.PreviousUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.PriceListId, e.ProductId, e.CreatedAt }).HasDatabaseName("IX_INV_PriceListItemHistory_List_Product_At");
        builder.HasIndex(e => e.ProductId).HasDatabaseName("IX_INV_PriceListItemHistory_ProductId");
    }
}
