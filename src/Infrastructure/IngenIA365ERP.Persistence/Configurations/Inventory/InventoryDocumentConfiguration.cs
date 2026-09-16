using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class InventoryDocumentConfiguration : IEntityTypeConfiguration<InventoryDocument>
{
    public void Configure(EntityTypeBuilder<InventoryDocument> builder)
    {
        builder.ToTable("INV_Documents");
        builder.HasOne(e => e.AccountingDocument).WithMany().HasForeignKey(e => e.AccountingDocumentId).OnDelete(DeleteBehavior.Restrict); // feature 009 (R16)
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.TransactionTypeId, e.SequenceNumber }).IsUnique();

        builder.Property(e => e.SequenceNumber).HasPrecision(18, 0);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);
        builder.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        builder.Property(e => e.VatAmount).HasPrecision(18, 2);
        builder.Property(e => e.CashAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);
        builder.Property(e => e.DebitCardAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditCardAmount).HasPrecision(18, 2);
        builder.Property(e => e.CheckAmount).HasPrecision(18, 2);
        builder.Property(e => e.AuditAmount).HasPrecision(18, 2);
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.BankId).HasMaxLength(10);
        builder.Property(e => e.BankAccountNumber).HasMaxLength(20);
        builder.Property(e => e.Detail).HasMaxLength(100);
        builder.Property(e => e.ReturnSequence).HasPrecision(18, 0);

        builder.HasOne(e => e.TransactionType).WithMany().HasForeignKey(e => e.TransactionTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
