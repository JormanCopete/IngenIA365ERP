using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_UnitsOfMeasure</c> (feature 012, T206; data-model §1.1): código único entre vivos; decimales admitidos 0..4 como
/// <c>tinyint</c>; código Rec. 20. Declara también la FK de la unidad de la línea del documento (<c>INV_DocumentLines.UnitId</c>),
/// que la fase 3 dejó para esta historia. La tabla llega con <c>InventarioComercialNucleo</c>.
/// </summary>
public class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ComoEntidadDeInventario("INV_UnitsOfMeasure");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Symbol).HasMaxLength(10);
        builder.Property(e => e.AllowedDecimals).IsRequired();
        builder.Property(e => e.DianUnitCode).HasMaxLength(3);
        builder.Property(e => e.IsSeeded).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_UnitsOfMeasure_Code").HasFilter("[IsDeleted] = 0");

        builder.HasMany<InventoryDocumentLine>().WithOne().HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);
    }
}
