using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.GoLive;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.GoLive;

/// <summary>
/// <c>INV_LegacyFigures</c> (feature 012, T307; data-model §6.5): las cifras de SOLIDO con sus códigos crudos, los Id resueltos
/// y el lote. Cantidades en <c>Cantidad</c>, valores en <c>Monto</c>. Sin migración propia (<see cref="NucleoComercialSinMigracion"/>).
/// </summary>
public class LegacyFigureConfiguration : IEntityTypeConfiguration<LegacyFigure>
{
    public void Configure(EntityTypeBuilder<LegacyFigure> builder)
    {
        builder.ComoEntidadDeInventario("INV_LegacyFigures");

        builder.Property(e => e.ImportBatchPublicId).IsRequired();
        builder.Property(e => e.AsOfDate).IsRequired();
        builder.Property(e => e.ProductCodeRaw).HasMaxLength(LegacyFigure.LargoDelCodigoDeProducto).IsRequired();
        builder.Property(e => e.WarehouseCodeRaw).HasMaxLength(LegacyFigure.LargoDelCodigoDeBodega).IsRequired();
        builder.Property(e => e.Quantity).Cantidad();
        builder.Property(e => e.QuantityIn).Cantidad();
        builder.Property(e => e.QuantityOut).Cantidad();
        builder.Property(e => e.Value).Monto();
        builder.Property(e => e.ValueIn).Monto();
        builder.Property(e => e.ValueOut).Monto();
        builder.Property(e => e.SourceFileName).HasMaxLength(LegacyFigure.LargoDelArchivo).IsRequired();

        builder.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingGroup>().WithMany().HasForeignKey(e => e.AccountingGroupId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.AsOfDate, e.WarehouseId }).HasDatabaseName("IX_INV_LegacyFigures_AsOf_Warehouse");
        builder.HasIndex(e => e.ImportBatchPublicId).HasDatabaseName("IX_INV_LegacyFigures_Batch");
    }
}
