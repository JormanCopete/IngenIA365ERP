using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_DayCloseLines</c> (feature 012, I3, T583; data-model §15): único <c>(DayCloseId, DetailKey)</c> entre vivos; importes en
/// pesos; FK <c>Restrict</c> al medio, al adquirente y al datáfono.
/// </summary>
public class DayCloseLineConfiguration : IEntityTypeConfiguration<DayCloseLine>
{
    public void Configure(EntityTypeBuilder<DayCloseLine> builder)
    {
        builder.ComoEntidadDeInventario("INV_DayCloseLines");

        builder.Property(e => e.DetailKey).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ExpectedAmount).Monto().IsRequired();
        builder.Property(e => e.CountedAmount).Monto().IsRequired();
        builder.Property(e => e.DifferenceAmount).Monto().IsRequired();
        builder.Property(e => e.BatchNumbersJson).HasMaxLength(400);

        builder.HasOne<DayClose>().WithMany(d => d.Lines).HasForeignKey(e => e.DayCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CardAcquirer>().WithMany().HasForeignKey(e => e.CardAcquirerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CardTerminal>().WithMany().HasForeignKey(e => e.CardTerminalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DayCloseId, e.DetailKey }).IsUnique()
            .HasDatabaseName("UK_INV_DayCloseLines_DayClose_DetailKey").HasFilter("[IsDeleted] = 0");
    }
}
