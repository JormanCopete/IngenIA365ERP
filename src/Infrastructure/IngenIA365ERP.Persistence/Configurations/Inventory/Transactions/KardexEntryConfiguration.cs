using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Transactions;

/// <summary>
/// <c>INV_KardexEntries</c> (feature 012, T250; data-model §3.1): hecho de alto volumen con <c>bigint</c> identidad, sólo
/// inserción (<c>IHechoInmutable</c>). <c>ReversesEntryId</c> y <c>AffectsEntryId</c> apuntan a la misma tabla; lote y serie
/// sin FK hasta I6. Índices del motor de costo y los retroactivos <c>(ProductId, CostScopeWarehouseId, OperationDate, Id)</c>, del
/// kardex por bodega y el valorizado a una fecha <c>(ProductId, WarehouseId, OperationDate, Id)</c>, por fecha, por documento y
/// el de ajustes por la entrada que corrigen (filtrado). Cantidad (18,4), costo unitario (18,6), monto (18,2). Nace con
/// <c>InventarioComercialNucleo</c> (T440); hasta entonces va excluida de las migraciones (<c>NucleoComercialSinMigracion</c>).
/// </summary>
public class KardexEntryConfiguration : IEntityTypeConfiguration<KardexEntry>
{
    public const string Tabla = "INV_KardexEntries";

    public void Configure(EntityTypeBuilder<KardexEntry> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");

        builder.Property(e => e.OperationDate).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasConversion<int>().IsRequired();
        builder.Property(e => e.QuantityBase).Cantidad().IsRequired();
        builder.Property(e => e.UnitCost).CostoUnitario().IsRequired();
        builder.Property(e => e.TotalCost).Monto().IsRequired();
        builder.Property(e => e.CostScopeWarehouseId).IsRequired();
        builder.Property(e => e.CostMethod).HasConversion<int>().IsRequired();

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(e => e.DocumentLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseLocation>().WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReversesEntry).WithMany().HasForeignKey(e => e.ReversesEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AffectsEntry).WithMany().HasForeignKey(e => e.AffectsEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.CostScopeWarehouseId, e.OperationDate, e.Id })
            .HasDatabaseName("IX_INV_KardexEntries_Product_Scope_Date");
        builder.HasIndex(e => new { e.ProductId, e.WarehouseId, e.OperationDate, e.Id })
            .HasDatabaseName("IX_INV_KardexEntries_Product_Warehouse_Date");
        builder.HasIndex(e => e.OperationDate).HasDatabaseName("IX_INV_KardexEntries_OperationDate");
        builder.HasIndex(e => e.DocumentId).HasDatabaseName("IX_INV_KardexEntries_DocumentId");
        builder.HasIndex(e => e.AffectsEntryId).HasDatabaseName("IX_INV_KardexEntries_AffectsEntryId")
            .HasFilter("[AffectsEntryId] IS NOT NULL");

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
