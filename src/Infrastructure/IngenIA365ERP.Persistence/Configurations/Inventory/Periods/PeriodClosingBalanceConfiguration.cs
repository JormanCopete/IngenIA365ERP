using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Periods;

/// <summary>
/// <c>INV_PeriodClosingBalances</c> (feature 012, T284; data-model §6.3): el valorizado fijado por producto × bodega en cada
/// versión de cierre. <c>UK (PeriodId, Version, ProductId, WarehouseId)</c> e <c>IX (PeriodId, Superseded)</c> para leer la
/// versión vigente; cantidad (18,4) y valor (18,2).
/// </summary>
public class PeriodClosingBalanceConfiguration : IEntityTypeConfiguration<PeriodClosingBalance>
{
    public void Configure(EntityTypeBuilder<PeriodClosingBalance> builder)
    {
        builder.ComoEntidadDeInventario("INV_PeriodClosingBalances");

        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.Quantity).Cantidad().IsRequired();
        builder.Property(e => e.Value).Monto().IsRequired();
        builder.Property(e => e.Superseded).IsRequired();

        builder.HasOne(e => e.Period).WithMany().HasForeignKey(e => e.PeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingGroup>().WithMany().HasForeignKey(e => e.AccountingGroupId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PeriodId, e.Version, e.ProductId, e.WarehouseId }).IsUnique()
            .HasDatabaseName("UK_INV_PeriodClosingBalances_Period_Version_Product_Warehouse");
        builder.HasIndex(e => new { e.PeriodId, e.Superseded }).HasDatabaseName("IX_INV_PeriodClosingBalances_Period_Superseded");
    }
}
