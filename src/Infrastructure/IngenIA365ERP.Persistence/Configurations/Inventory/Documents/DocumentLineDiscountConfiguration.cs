using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Documents;

/// <summary>
/// <c>INV_DocumentLineDiscounts</c> (feature 012, I3, T584; FR-054; data-model §14): único <c>(DocumentLineId, Sequence)</c> entre
/// vivos; fracciones (9,6) y valor en pesos (18,2); FK <c>Restrict</c> a la línea, al documento, a la solicitud de aprobación y al
/// aprobador; desde I6 (<c>ComercioAmpliado</c>) también a la promoción.
/// </summary>
public class DocumentLineDiscountConfiguration : IEntityTypeConfiguration<DocumentLineDiscount>
{
    public void Configure(EntityTypeBuilder<DocumentLineDiscount> builder)
    {
        builder.ComoEntidadDeInventario("INV_DocumentLineDiscounts");

        builder.Property(e => e.Source).IsRequired();
        builder.Property(e => e.Rate).Tarifa();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.CapRateApplied).Tarifa().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(200);

        builder.HasOne(e => e.DocumentLine).WithMany().HasForeignKey(e => e.DocumentLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalRequest>().WithMany().HasForeignKey(e => e.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        // I6 (ComercioAmpliado, T858; data-model §14): la promoción que produjo el descuento.
        builder.HasOne(e => e.Promotion).WithMany().HasForeignKey(e => e.PromotionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentLineId, e.Sequence }).IsUnique()
            .HasDatabaseName("UK_INV_DocumentLineDiscounts_Line_Sequence").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.DocumentId).HasDatabaseName("IX_INV_DocumentLineDiscounts_DocumentId");
    }
}
