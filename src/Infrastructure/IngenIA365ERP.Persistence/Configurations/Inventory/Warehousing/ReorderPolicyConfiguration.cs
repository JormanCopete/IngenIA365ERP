using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Warehousing;

/// <summary>
/// <c>INV_ReorderPolicies</c> (feature 012, T208; FR-035; data-model §2.4): una política viva por (producto, bodega) —darla
/// de baja y volver a fijarla crea una fila nueva—, FK <c>Restrict</c> a producto y bodega, cantidades (18,4).
/// </summary>
public class ReorderPolicyConfiguration : IEntityTypeConfiguration<ReorderPolicy>
{
    public void Configure(EntityTypeBuilder<ReorderPolicy> builder)
    {
        builder.ComoEntidadDeInventario("INV_ReorderPolicies");

        builder.Property(e => e.MinimumQuantity).Cantidad().IsRequired();
        builder.Property(e => e.MaximumQuantity).Cantidad().IsRequired();
        builder.Property(e => e.ReorderPoint).Cantidad().IsRequired();

        builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.WarehouseId })
            .IsUnique().HasDatabaseName("UK_INV_ReorderPolicies_Product_Warehouse").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.WarehouseId).HasDatabaseName("IX_INV_ReorderPolicies_WarehouseId");
    }
}
