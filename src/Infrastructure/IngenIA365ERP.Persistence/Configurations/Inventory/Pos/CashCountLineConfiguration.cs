using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashCountLines</c> (feature 012, I3, T583; data-model §15): único <c>(CashCountId, PaymentMeansId)</c> entre vivos;
/// importes en pesos.
/// </summary>
public class CashCountLineConfiguration : IEntityTypeConfiguration<CashCountLine>
{
    public void Configure(EntityTypeBuilder<CashCountLine> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashCountLines");

        builder.Property(e => e.CountMethod).IsRequired();
        builder.Property(e => e.ExpectedAmount).Monto().IsRequired();
        builder.Property(e => e.CountedAmount).Monto().IsRequired();
        builder.Property(e => e.DifferenceAmount).Monto().IsRequired();
        builder.Property(e => e.ToleranceAmount).Monto().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300);
        builder.Property(e => e.Status).HasDefaultValue(DocumentStatus.Draft).IsRequired();

        builder.HasOne(e => e.CashCount).WithMany(c => c.Lines).HasForeignKey(e => e.CashCountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CashCountId, e.PaymentMeansId }).IsUnique()
            .HasDatabaseName("UK_INV_CashCountLines_Count_Means").HasFilter("[IsDeleted] = 0");
    }
}
