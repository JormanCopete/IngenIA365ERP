using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class InventoryInvoiceConfiguration : IEntityTypeConfiguration<InventoryInvoice>
{
    public void Configure(EntityTypeBuilder<InventoryInvoice> builder)
    {
        builder.ToTable("INV_Invoices");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.InvoiceCode).IsUnique();

        builder.Property(e => e.InvoiceCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Resolution).HasMaxLength(20);
        builder.Property(e => e.Prefix).HasMaxLength(10);
        builder.Property(e => e.InitialNumber).HasMaxLength(20);
        builder.Property(e => e.FinalNumber).HasMaxLength(20);
        builder.Property(e => e.EmployeeVoucherCode).HasMaxLength(5);
        builder.Property(e => e.EmployeeDeductionType).HasMaxLength(2);
        builder.Property(e => e.EmployerVoucherCode).HasMaxLength(5);
        builder.Property(e => e.EmployerDeductionType).HasMaxLength(2);
        builder.Property(e => e.PortfolioVoucherCode).HasMaxLength(5);
        builder.Property(e => e.DeductionType).HasMaxLength(2);
        builder.Property(e => e.CommissionGroup).HasMaxLength(2);
        builder.Property(e => e.CommissionWithholdingRate).HasPrecision(4, 2);
        builder.Property(e => e.ThirdPartySpecialLineId).HasMaxLength(5);
        builder.Property(e => e.ThirdPartyCreditLineId).HasMaxLength(5);
        builder.Property(e => e.ThirdPartySpecialVoucher).HasMaxLength(5);
        builder.Property(e => e.ThirdPartyVoucherCode).HasMaxLength(5);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
