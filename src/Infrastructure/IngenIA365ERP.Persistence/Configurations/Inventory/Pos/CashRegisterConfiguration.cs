using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashRegisters</c> (feature 012, I3, T583; data-model §15): único <c>(PointOfSaleId, Code)</c> entre vivos; FK
/// <c>Restrict</c> a bodega y datáfono por defecto. Declara también la FK de <c>INV_Documents.CashRegisterId</c>.
/// </summary>
public class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashRegisters");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.DianCashRegisterPlate).HasMaxLength(30);
        builder.Property(e => e.DianCashRegisterTypeCode).HasMaxLength(10);
        builder.Property(e => e.ReceiptWidthMm).HasDefaultValue((short)80);
        builder.Property(e => e.PrintCopies).HasDefaultValue((byte)1);
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasOne(e => e.PointOfSale).WithMany(p => p.CashRegisters).HasForeignKey(e => e.PointOfSaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CardTerminal>().WithMany().HasForeignKey(e => e.DefaultCardTerminalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocument>().WithOne().HasForeignKey(d => d.CashRegisterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PointOfSaleId, e.Code }).IsUnique()
            .HasDatabaseName("UK_INV_CashRegisters_Point_Code").HasFilter("[IsDeleted] = 0");
    }
}
