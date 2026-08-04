using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class PriceConfiguration : IEntityTypeConfiguration<Price>
{
    public void Configure(EntityTypeBuilder<Price> builder)
    {
        builder.ToTable("INV_Prices");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.PriceListTypeId, e.ProductId, e.CustomerType }).IsUnique();

        builder.Property(e => e.CustomerType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.PriceValue).HasPrecision(18, 2);
        builder.Property(e => e.Description).HasMaxLength(50);

        builder.HasOne(e => e.PriceListType).WithMany().HasForeignKey(e => e.PriceListTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
