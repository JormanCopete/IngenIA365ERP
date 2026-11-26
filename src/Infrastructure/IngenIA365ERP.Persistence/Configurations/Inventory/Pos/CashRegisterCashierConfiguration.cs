using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashRegisterCashiers</c> (2026-11-25, pedido del dueño): los cajeros de una caja del punto de venta, como <c>RCD_CashBoxCashiers</c>. Único
/// <c>(CashRegisterId, UserId, ValidFrom)</c> entre vivos; FK <c>Restrict</c> a la caja y al usuario. (nuevo)
/// </summary>
public class CashRegisterCashierConfiguration : IEntityTypeConfiguration<CashRegisterCashier>
{
    public const string Tabla = "INV_CashRegisterCashiers";

    public void Configure(EntityTypeBuilder<CashRegisterCashier> builder)
    {
        builder.ComoEntidadDeInventario(Tabla);
        builder.HasOne<CashRegister>().WithMany().HasForeignKey(e => e.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CashRegisterId, e.UserId, e.ValidFrom }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_CashRegisterId_UserId_ValidFrom");
        builder.HasIndex(e => e.UserId).HasDatabaseName($"IX_{Tabla}_UserId");
    }
}
