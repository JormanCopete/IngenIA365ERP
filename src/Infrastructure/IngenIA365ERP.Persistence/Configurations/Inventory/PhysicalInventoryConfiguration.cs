using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class PhysicalInventoryConfiguration : IEntityTypeConfiguration<PhysicalInventory>
{
    public void Configure(EntityTypeBuilder<PhysicalInventory> builder)
    {
        builder.ToTable("INV_PhysicalInventory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.PeriodCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Cost).HasPrecision(18, 2);

        builder.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
