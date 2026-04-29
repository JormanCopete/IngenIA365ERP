using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PortfolioInvoiceConfiguration : IEntityTypeConfiguration<PortfolioInvoice>
{
    public void Configure(EntityTypeBuilder<PortfolioInvoice> builder)
    {
        builder.ToTable("LND_PortfolioInvoices");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PortfolioInvoices_PublicId");

        builder.Property(e => e.Description).HasMaxLength(120).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.VoucherType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.InvoiceNumber).HasPrecision(20, 0);
        builder.Property(e => e.Amount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
