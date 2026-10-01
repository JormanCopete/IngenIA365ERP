using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pricing;

/// <summary>
/// <c>INV_Promotions</c> (feature 012, I6, T858; data-model §14 «Promociones»): código de 10 único entre vivos
/// (<c>CodigoDeCatalogo</c>), clase como int, tarifa (9,6), valores en pesos (18,2) y cantidades del 3×2 (18,4); vigencia como
/// <c>date</c>. Nace con <c>ComercioAmpliado</c>.
/// </summary>
public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ComoEntidadDeInventario("INV_Promotions");

        builder.Property(e => e.Code).HasMaxLength(Promotion.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(Promotion.LargoDelNombre).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Rate).Tarifa();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.BuyQuantity).Cantidad();
        builder.Property(e => e.PayQuantity).Cantidad();
        builder.Property(e => e.BundlePrice).Monto();
        builder.Property(e => e.IsCumulative).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(Promotion.LargoDeLasNotas);

        builder.HasMany(e => e.Scopes).WithOne(s => s.Promotion).HasForeignKey(s => s.PromotionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Tiers).WithOne(t => t.Promotion).HasForeignKey(t => t.PromotionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_Promotions_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.ValidFrom, e.ValidTo }).HasDatabaseName("IX_INV_Promotions_Validity");
    }
}
