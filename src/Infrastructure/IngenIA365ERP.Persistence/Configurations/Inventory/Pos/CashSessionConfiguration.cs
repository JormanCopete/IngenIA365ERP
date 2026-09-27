using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashSessions</c> (feature 012, I3, T583; T50; data-model §15): una sesión abierta por caja
/// (<c>UK_INV_CashSessions_Register_Open</c>) y, cuando la sesión selló <c>ExclusiveCashier</c>, una por cajero
/// (<c>UK_INV_CashSessions_Cashier_Open</c>); sus colisiones las traduce <c>ColisionesDeVenta</c> (T589). Índice
/// <c>(PointOfSaleId, OperatingDate)</c> para el cierre del día. Declara también la FK de <c>INV_Documents.CashSessionId</c>.
/// </summary>
public class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashSessions");

        builder.Property(e => e.CashierName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(30);
        builder.Property(e => e.BaseMode).HasMaxLength(12).IsRequired();
        builder.Property(e => e.OpeningBase).Monto().IsRequired();
        builder.Property(e => e.ShortageTreatment).HasMaxLength(14).IsRequired();
        builder.Property(e => e.Status).IsRequired();

        builder.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PointOfSale>().WithMany().HasForeignKey(e => e.PointOfSaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.CashierPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.BaseIncomeDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocument>().WithOne().HasForeignKey(d => d.CashSessionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CashRegisterId, "UK_INV_CashSessions_Register_Open")
            .IsUnique().HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.CashierUserId, "UK_INV_CashSessions_Cashier_Open")
            .IsUnique().HasFilter("[Status] = 1 AND [ExclusiveCashier] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.PointOfSaleId, e.OperatingDate }).HasDatabaseName("IX_INV_CashSessions_Point_OperatingDate");
    }
}
