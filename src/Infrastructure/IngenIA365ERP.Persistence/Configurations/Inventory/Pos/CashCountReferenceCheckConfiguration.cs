using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashCountReferenceChecks</c> (feature 012, I3, T583; data-model §15): único <c>(CashCountLineId, DocumentPaymentId)</c>
/// entre vivos.
/// </summary>
public class CashCountReferenceCheckConfiguration : IEntityTypeConfiguration<CashCountReferenceCheck>
{
    public void Configure(EntityTypeBuilder<CashCountReferenceCheck> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashCountReferenceChecks");

        builder.Property(e => e.Note).HasMaxLength(200);

        builder.HasOne<CashCountLine>().WithMany(l => l.ReferenceChecks).HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentPayment>().WithMany().HasForeignKey(e => e.DocumentPaymentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CashCountLineId, e.DocumentPaymentId }).IsUnique()
            .HasDatabaseName("UK_INV_CashCountReferenceChecks_Line_Payment").HasFilter("[IsDeleted] = 0");
    }
}
