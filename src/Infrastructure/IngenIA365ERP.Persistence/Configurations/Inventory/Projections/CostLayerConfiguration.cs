using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Projections;

/// <summary>
/// <c>INV_CostLayers</c> (feature 012, I5, T833; data-model §3.5): la proyección de capas PEPS, <c>bigint</c> como el kardex. Única
/// por la línea que la creó (<c>UK_INV_CostLayers_EntryKardexEntryId</c>) y ordenada para PEPS por
/// <c>IX (ProductId, ScopeWarehouseId, OperationDate, EntryKardexEntryId)</c>. <c>ScopeWarehouseId</c> sin FK (0 = cooperativa).
/// Cantidades (18,4), costo (18,6). Adelantada por la sección de dominio de costeo (la exige
/// <c>LasCantidadesYCostosTienenSuPrecision</c>); el DbSet y la migración <c>ComprasYCosteoAvanzado</c> son de la sección de
/// persistencia (T835).
/// </summary>
public class CostLayerConfiguration : IEntityTypeConfiguration<CostLayer>
{
    public const string Tabla = "INV_CostLayers";

    public void Configure(EntityTypeBuilder<CostLayer> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");

        builder.Property(e => e.ScopeWarehouseId).IsRequired();
        builder.Property(e => e.OperationDate).IsRequired();
        builder.Property(e => e.OriginalQuantity).Cantidad().IsRequired();
        builder.Property(e => e.RemainingQuantity).Cantidad().IsRequired();
        builder.Property(e => e.UnitCost).CostoUnitario().IsRequired();

        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KardexEntry>().WithMany().HasForeignKey(e => e.EntryKardexEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.EntryKardexEntryId).IsUnique().HasDatabaseName("UK_INV_CostLayers_EntryKardexEntryId");
        builder.HasIndex(e => new { e.ProductId, e.ScopeWarehouseId, e.OperationDate, e.EntryKardexEntryId })
            .HasDatabaseName("IX_INV_CostLayers_Product_Scope_Order");

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
