using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pricing;

/// <summary>
/// <c>INV_PromotionTiers</c> (feature 012, I6, T858; data-model §14): único <c>(PromotionId, MinQuantity)</c> entre vivos; cantidad
/// (18,4) y precio unitario en pesos (18,2).
/// </summary>
public class PromotionTierConfiguration : IEntityTypeConfiguration<PromotionTier>
{
    public void Configure(EntityTypeBuilder<PromotionTier> builder)
    {
        builder.ComoEntidadDeInventario("INV_PromotionTiers");

        builder.Property(e => e.MinQuantity).Cantidad().IsRequired();
        builder.Property(e => e.UnitPrice).Monto().IsRequired();

        builder.HasIndex(e => new { e.PromotionId, e.MinQuantity }).IsUnique()
            .HasDatabaseName("UK_INV_PromotionTiers_Promotion_MinQuantity").HasFilter("[IsDeleted] = 0");
    }
}
