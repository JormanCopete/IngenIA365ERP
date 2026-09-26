using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Documents;

/// <summary>
/// <c>INV_CountCaptures</c> (feature 012, US11, T391; data-model §8.2): las tandas de lecturas de cada contador, sólo inserción
/// (<c>IHechoInmutable</c>). <c>IX_INV_CountCaptures_Document_Round_Line (DocumentId, Round, SnapshotLineId)</c> es el que suma
/// lo contado por ronda. Cantidad con <c>PrecisionDeInventario.Cantidad</c>. Sin migración propia: entra al par
/// <c>InventarioComercialNucleo</c> (T440). (nuevo)
/// </summary>
public class CountCaptureConfiguration : IEntityTypeConfiguration<CountCapture>
{
    public void Configure(EntityTypeBuilder<CountCapture> builder)
    {
        builder.ComoEntidadDeInventario("INV_CountCaptures");

        builder.Property(e => e.Round).IsRequired();
        builder.Property(e => e.Quantity).Cantidad().IsRequired();
        builder.Property(e => e.Reads).IsRequired();
        builder.Property(e => e.IsCorrection).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CapturedAt).IsRequired();

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CounterUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentId, e.Round, e.SnapshotLineId }).HasDatabaseName("IX_INV_CountCaptures_Document_Round_Line");
    }
}
