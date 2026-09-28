using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pricing;

/// <summary>
/// <c>INV_PriceListItems</c> (feature 012, I3, T584; data-model §14): único <c>(PriceListId, ProductId, UnitId)</c> entre vivos;
/// precio en pesos (18,2).
/// </summary>
public class PriceListItemConfiguration : IEntityTypeConfiguration<PriceListItem>
{
    public void Configure(EntityTypeBuilder<PriceListItem> builder)
    {
        builder.ComoEntidadDeInventario("INV_PriceListItems");

        builder.Property(e => e.Price).Monto().IsRequired();

        builder.HasOne(e => e.PriceList).WithMany(p => p.Items).HasForeignKey(e => e.PriceListId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PriceListId, e.ProductId, e.UnitId }).IsUnique()
            .HasDatabaseName("UK_INV_PriceListItems_List_Product_Unit").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.ProductId).HasDatabaseName("IX_INV_PriceListItems_ProductId");
    }
}
