using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("INV_Products");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.ProductCode).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(80);
        builder.Property(e => e.UnitOfMeasure).HasMaxLength(10);
        builder.Property(e => e.Barcode).HasMaxLength(30);
        builder.Property(e => e.CostPrice).HasPrecision(18, 2);
        builder.Property(e => e.SalePrice).HasPrecision(18, 2);
        builder.Property(e => e.VatRate).HasPrecision(6, 3);
        builder.Property(e => e.OtherTax).HasPrecision(10, 2);

        builder.HasOne(e => e.Group).WithMany().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DiscountType).WithMany().HasForeignKey(e => e.DiscountTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
