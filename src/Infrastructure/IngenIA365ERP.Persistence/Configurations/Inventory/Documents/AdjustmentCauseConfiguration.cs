using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Documents;

/// <summary>
/// <c>INV_AdjustmentCauses</c> (feature 012, T207; data-model §5.10): código único entre vivos e inmutable (T27). Declara
/// también la FK de la causa de la línea del documento (<c>INV_DocumentLines.AdjustmentCauseId</c>).
/// </summary>
public class AdjustmentCauseConfiguration : IEntityTypeConfiguration<AdjustmentCause>
{
    public void Configure(EntityTypeBuilder<AdjustmentCause> builder)
    {
        builder.ComoEntidadDeInventario("INV_AdjustmentCauses");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.AllowsPositive).IsRequired();
        builder.Property(e => e.AllowsNegative).IsRequired();
        builder.Property(e => e.AllowsTransitWriteOff).IsRequired();
        builder.Property(e => e.RequiresAttachment).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.IsSeeded).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.IsRequiredBySystem).HasDefaultValue(false).IsRequired();

        builder.HasMany<InventoryDocumentLine>().WithOne().HasForeignKey(l => l.AdjustmentCauseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_AdjustmentCauses_Code").HasFilter("[IsDeleted] = 0");
    }
}
