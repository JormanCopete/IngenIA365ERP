using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_ProductAccountingGroupChanges</c> (feature 012, T206; data-model §1.10): historial del grupo contable, hecho
/// inmutable (sólo inserción, lo hace cumplir <c>ApplicationDbContext.SaveChangesAsync</c> por <c>IHechoInmutable</c>).
/// Cantidad y valor con su precisión; índice por (producto, fecha efectiva) para leer el grupo a una fecha.
/// </summary>
public class ProductAccountingGroupChangeConfiguration : IEntityTypeConfiguration<ProductAccountingGroupChange>
{
    public void Configure(EntityTypeBuilder<ProductAccountingGroupChange> builder)
    {
        builder.ComoEntidadDeInventario("INV_ProductAccountingGroupChanges");

        builder.Property(e => e.EffectiveDate).IsRequired();
        builder.Property(e => e.Quantity).Cantidad().IsRequired();
        builder.Property(e => e.Value).Monto().IsRequired();
        builder.Property(e => e.DetailJson).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();

        builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingGroup>().WithMany().HasForeignKey(e => e.FromAccountingGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingGroup>().WithMany().HasForeignKey(e => e.ToAccountingGroupId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.EffectiveDate }).HasDatabaseName("IX_INV_ProductAccountingGroupChanges_Product_Date");
    }
}
