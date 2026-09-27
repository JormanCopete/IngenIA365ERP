using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashCountDenominations</c> (feature 012, I3, T583; data-model §15): único <c>(CashCountLineId, CashDenominationId)</c>
/// entre vivos; valor y total en pesos.
/// </summary>
public class CashCountDenominationConfiguration : IEntityTypeConfiguration<CashCountDenomination>
{
    public void Configure(EntityTypeBuilder<CashCountDenomination> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashCountDenominations");

        builder.Property(e => e.DenominationValue).Monto().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();

        builder.HasOne<CashCountLine>().WithMany(l => l.Denominations).HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashDenomination>().WithMany().HasForeignKey(e => e.CashDenominationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CashCountLineId, e.CashDenominationId }).IsUnique()
            .HasDatabaseName("UK_INV_CashCountDenominations_Line_Denomination").HasFilter("[IsDeleted] = 0");
    }
}
