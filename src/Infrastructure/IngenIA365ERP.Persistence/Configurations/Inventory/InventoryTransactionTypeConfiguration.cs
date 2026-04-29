using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class InventoryTransactionTypeConfiguration : IEntityTypeConfiguration<InventoryTransactionType>
{
    public void Configure(EntityTypeBuilder<InventoryTransactionType> builder)
    {
        builder.ToTable("INV_TransactionTypes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.TypeCode).IsUnique();

        builder.Property(e => e.Description).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ShortDescription).HasMaxLength(80);
        builder.Property(e => e.TransactionVoucherCode).HasMaxLength(5);
        builder.Property(e => e.CostVoucherCode).HasMaxLength(5);
        builder.Property(e => e.SequenceNumber).HasPrecision(18, 0);
        builder.Property(e => e.DocumentClass).HasMaxLength(5);
        builder.Property(e => e.PortfolioVoucherCode).HasMaxLength(5);
        builder.Property(e => e.DeductionType).HasMaxLength(2);
        builder.Property(e => e.InvoiceControl).HasMaxLength(5);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
