using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_DocumentTypeWarehouses</c> (feature 012, FR-037, T136; data-model §5.8). Único <c>(DocumentTypeId,
/// WarehouseId)</c> entre vivos. La FK a <c>INV_Warehouses</c> la declara la configuración de la bodega (US1).
/// </summary>
public class DocumentTypeWarehouseConfiguration : IEntityTypeConfiguration<DocumentTypeWarehouse>
{
    public void Configure(EntityTypeBuilder<DocumentTypeWarehouse> builder)
    {
        builder.ToTable("INV_DocumentTypeWarehouses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_DocumentTypeWarehouses_PublicId");

        builder.Property(e => e.WarehouseId).IsRequired();

        builder.HasIndex(e => new { e.DocumentTypeId, e.WarehouseId })
            .IsUnique()
            .HasDatabaseName("UK_INV_DocumentTypeWarehouses_Type_Warehouse")
            .HasFilter("[IsDeleted] = 0");

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
