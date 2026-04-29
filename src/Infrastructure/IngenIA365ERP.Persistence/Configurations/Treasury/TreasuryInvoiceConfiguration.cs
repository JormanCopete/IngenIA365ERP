using IngenIA365ERP.Domain.Entities.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury;

public class TreasuryInvoiceConfiguration : IEntityTypeConfiguration<TreasuryInvoice>
{
    public void Configure(EntityTypeBuilder<TreasuryInvoice> builder)
    {
        builder.ToTable("TRS_Invoices");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.InvoiceNumber, e.PersonId, e.AccountCode }).IsUnique();

        builder.Property(e => e.ConceptCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AccountCode).HasMaxLength(15);
        builder.Property(e => e.CostCenterId).HasMaxLength(10);
        builder.Property(e => e.BranchId).HasMaxLength(10);
        builder.Property(e => e.DocumentCode).HasMaxLength(15);
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.VoucherCode).HasMaxLength(5);
        builder.Property(e => e.BankId).HasMaxLength(10);
        builder.Property(e => e.CheckAmount).HasPrecision(18, 2);
        builder.Property(e => e.Status).HasMaxLength(2);
        builder.Property(e => e.DocumentType).HasMaxLength(5);
        builder.Property(e => e.DocumentNumber).HasMaxLength(20);
        builder.Property(e => e.PaymentForm).HasMaxLength(5);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
