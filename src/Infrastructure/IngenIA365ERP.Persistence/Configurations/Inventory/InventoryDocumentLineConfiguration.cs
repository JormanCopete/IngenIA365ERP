using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_DocumentLines</c> (feature 012, T17, T136; data-model §5.4). Único <c>(DocumentId, LineNumber)</c> e índice
/// por producto. Cantidades (18,4), factor (18,6), precio y costo unitario (18,6), montos (18,2). Las FK a producto,
/// unidad, ubicación y causa las declara la configuración de esas entidades (US1); lote y serie no tienen FK hasta I6.
/// </summary>
public class InventoryDocumentLineConfiguration : IEntityTypeConfiguration<InventoryDocumentLine>
{
    public void Configure(EntityTypeBuilder<InventoryDocumentLine> builder)
    {
        builder.ToTable("INV_DocumentLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_DocumentLines_PublicId");

        builder.Property(e => e.LineNumber).IsRequired();
        builder.Property(e => e.ProductId).IsRequired();
        builder.Property(e => e.UnitId).IsRequired();
        builder.Property(e => e.Quantity).Cantidad().IsRequired();
        builder.Property(e => e.Factor).Factor().IsRequired();
        builder.Property(e => e.QuantityBase).Cantidad().IsRequired();
        builder.Property(e => e.RoundingQuantity).Cantidad().IsRequired();
        builder.Property(e => e.UnitPrice).PrecioUnitario().IsRequired();
        builder.Property(e => e.ListPriceIncludesTaxes).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.GrossAmount).Monto().IsRequired();
        builder.Property(e => e.DiscountAmount).Monto().IsRequired();
        builder.Property(e => e.NetAmount).Monto().IsRequired();
        builder.Property(e => e.UnitCost).CostoUnitario();
        builder.Property(e => e.TotalCost).Monto();
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.AffectsCost).HasDefaultValue(false);

        // Filtrado a las vivas (fase 3, ciclo común): reemplazar las líneas de un borrador da de baja las que faltan y
        // renumera las demás desde 1, así el número de una línea de baja se reusa.
        builder.HasIndex(e => new { e.DocumentId, e.LineNumber }).IsUnique().HasDatabaseName("UK_INV_DocumentLines_Document_LineNumber")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.ProductId).HasDatabaseName("IX_INV_DocumentLines_ProductId");

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
