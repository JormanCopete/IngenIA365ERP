using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class InventoryDiscountConfiguration : IEntityTypeConfiguration<InventoryDiscount>
{
    public void Configure(EntityTypeBuilder<InventoryDiscount> builder)
    {
        builder.ToTable("INV_Discounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CustomerId).HasMaxLength(20);
        builder.Property(e => e.ProductClass).HasMaxLength(5);
        builder.Property(e => e.CustomerType).HasMaxLength(5);
        builder.Property(e => e.DiscountRate).HasPrecision(6, 3);
        builder.Property(e => e.GroupId).HasMaxLength(10);

        builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DiscountType).WithMany().HasForeignKey(e => e.DiscountTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
