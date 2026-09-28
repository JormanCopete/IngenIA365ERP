using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Documents;

/// <summary>
/// <c>INV_VoucherRedemptions</c> (feature 012, I3, T584; FR-097, SC-002; data-model §16):
/// <c>UK_INV_VoucherRedemptions_Means_Number_Active</c> filtrado a activos vivos garantiza en la base que un bono de número único
/// no se use en dos cajas a la vez; su colisión la traduce <c>ColisionesDeVenta</c> (T589) a <c>Payments.VoucherAlreadyUsed</c>.
/// </summary>
public class VoucherRedemptionConfiguration : IEntityTypeConfiguration<VoucherRedemption>
{
    public void Configure(EntityTypeBuilder<VoucherRedemption> builder)
    {
        builder.ComoEntidadDeInventario("INV_VoucherRedemptions");

        builder.Property(e => e.NormalizedNumber).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ReleaseReason).HasMaxLength(200);

        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DocumentPayment).WithMany().HasForeignKey(e => e.DocumentPaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.ReleasedByDocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PaymentMeansId, e.NormalizedNumber }, "UK_INV_VoucherRedemptions_Means_Number_Active")
            .IsUnique().HasFilter("[Status] = 1 AND [IsDeleted] = 0");
    }
}
