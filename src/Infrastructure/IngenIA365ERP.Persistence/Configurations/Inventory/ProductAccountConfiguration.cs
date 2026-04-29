using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class ProductAccountConfiguration : IEntityTypeConfiguration<ProductAccount>
{
    public void Configure(EntityTypeBuilder<ProductAccount> builder)
    {
        builder.ToTable("INV_ProductAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.ProductGroupId, e.TransactionTypeId, e.WarehouseId, e.LocationId }).IsUnique();

        builder.Property(e => e.VatAccountCode).HasMaxLength(15);
        builder.Property(e => e.DiscountAccountCode).HasMaxLength(15);
        builder.Property(e => e.TaxableSalesAccountCode).HasMaxLength(15);
        builder.Property(e => e.NonTaxableSalesAccountCode).HasMaxLength(15);
        builder.Property(e => e.NetAccountCode).HasMaxLength(15);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
