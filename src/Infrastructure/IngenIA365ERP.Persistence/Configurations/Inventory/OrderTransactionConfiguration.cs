using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class OrderTransactionConfiguration : IEntityTypeConfiguration<OrderTransaction>
{
    public void Configure(EntityTypeBuilder<OrderTransaction> builder)
    {
        builder.ToTable("INV_OrderTransactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.SequenceNumber).HasPrecision(18, 0);
        builder.Property(e => e.InvoiceNumber).HasMaxLength(20);
        builder.Property(e => e.Quantity).HasPrecision(18, 3);
        builder.Property(e => e.VatRate).HasPrecision(6, 3);
        builder.Property(e => e.DiscountRate).HasPrecision(6, 3);
        builder.Property(e => e.CostAmount).HasPrecision(16, 2);
        builder.Property(e => e.VatAmount).HasPrecision(18, 3);
        builder.Property(e => e.DiscountAmount).HasPrecision(18, 3);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.SubTotal).HasPrecision(18, 3);
        builder.Property(e => e.NetTotal).HasPrecision(18, 3);
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.SaleType).HasMaxLength(5);
        builder.Property(e => e.MovementClass).HasMaxLength(5);
        builder.Property(e => e.AdminFee).HasPrecision(10, 2);
        builder.Property(e => e.AdminVat).HasPrecision(10, 2);
        builder.Property(e => e.TicketVat).HasPrecision(10, 2);
        builder.Property(e => e.OtherTax).HasPrecision(10, 2);
        builder.Property(e => e.AirportTax).HasPrecision(10, 2);
        builder.Property(e => e.FuelTax).HasPrecision(10, 2);
        builder.Property(e => e.WithholdingRate).HasPrecision(6, 3);
        builder.Property(e => e.WithholdingAmount).HasPrecision(15, 2);
        builder.Property(e => e.IcaAmount).HasPrecision(15, 2);
        builder.Property(e => e.IcaRate).HasPrecision(6, 3);
        builder.Property(e => e.TransferRecord).HasMaxLength(100);

        builder.HasOne(e => e.TransactionType).WithMany().HasForeignKey(e => e.TransactionTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
