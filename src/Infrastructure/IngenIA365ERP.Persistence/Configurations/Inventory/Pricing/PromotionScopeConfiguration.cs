using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pricing;

/// <summary>
/// <c>INV_PromotionScopes</c> (feature 012, I6, T858; data-model §14): una sola columna destino llena por fila según <c>ScopeKind</c>,
/// que fija el <c>CHECK</c> <see cref="ChequeoDeAlcance"/> (escrito en T-SQL con corchetes; <c>ProviderModelConventions</c> lo traduce a
/// PostgreSQL, como los filtros de índice). FK <c>Restrict</c> a producto, categoría y canal.
/// </summary>
public class PromotionScopeConfiguration : IEntityTypeConfiguration<PromotionScope>
{
    public const string ChequeoDeAlcance = "CK_INV_PromotionScopes_OneTarget";

    /// <summary>
    /// Una columna destino por clase (<c>PromotionScopeKind</c>: 1 producto, 2 categoría, 3 segmento, 4 canal) y las demás nulas.
    /// </summary>
    public const string SqlDelChequeo =
        "([ScopeKind] = 1 AND [ProductId] IS NOT NULL AND [ProductCategoryId] IS NULL AND [Segment] IS NULL AND [SalesChannelId] IS NULL)"
        + " OR ([ScopeKind] = 2 AND [ProductId] IS NULL AND [ProductCategoryId] IS NOT NULL AND [Segment] IS NULL AND [SalesChannelId] IS NULL)"
        + " OR ([ScopeKind] = 3 AND [ProductId] IS NULL AND [ProductCategoryId] IS NULL AND [Segment] IS NOT NULL AND [SalesChannelId] IS NULL)"
        + " OR ([ScopeKind] = 4 AND [ProductId] IS NULL AND [ProductCategoryId] IS NULL AND [Segment] IS NULL AND [SalesChannelId] IS NOT NULL)";

    public void Configure(EntityTypeBuilder<PromotionScope> builder)
    {
        builder.ComoEntidadDeInventario("INV_PromotionScopes");
        builder.ToTable(t => t.HasCheckConstraint(ChequeoDeAlcance, SqlDelChequeo));

        builder.Property(e => e.ScopeKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Segment).HasMaxLength(PromotionScope.LargoDelSegmento);
        builder.Property(e => e.RequiredQuantity).Cantidad();

        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductCategory>().WithMany().HasForeignKey(e => e.ProductCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesChannel>().WithMany().HasForeignKey(e => e.SalesChannelId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PromotionId, e.ScopeKind }).HasDatabaseName("IX_INV_PromotionScopes_Promotion_Kind");
    }
}
