using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_DayCloses</c> (feature 012, I3, T583; FR-099; data-model §15): un solo cierre vigente por punto y fecha
/// (<c>UK_INV_DayCloses_Point_Date_Closed</c>, filtrado a <c>Closed</c>; su colisión la traduce <c>ColisionesDeVenta</c> a
/// <c>Inventory.DayClose.AlreadyClosed</c>) y una versión por reapertura (<c>(PointOfSaleId, OperatingDate, Version)</c>).
/// </summary>
public class DayCloseConfiguration : IEntityTypeConfiguration<DayClose>
{
    public void Configure(EntityTypeBuilder<DayClose> builder)
    {
        builder.ComoEntidadDeInventario("INV_DayCloses");

        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.TotalExpected).Monto().IsRequired();
        builder.Property(e => e.TotalCounted).Monto().IsRequired();
        builder.Property(e => e.TotalDifference).Monto().IsRequired();
        builder.Property(e => e.ReopenReason).HasMaxLength(300);

        builder.HasOne(e => e.PointOfSale).WithMany().HasForeignKey(e => e.PointOfSaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PointOfSaleId, e.OperatingDate }, "UK_INV_DayCloses_Point_Date_Closed")
            .IsUnique().HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.PointOfSaleId, e.OperatingDate, e.Version }, "UK_INV_DayCloses_Point_Date_Version")
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
