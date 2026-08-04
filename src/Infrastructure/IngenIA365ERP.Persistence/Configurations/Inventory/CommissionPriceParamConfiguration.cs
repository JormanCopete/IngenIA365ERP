using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class CommissionPriceParamConfiguration : IEntityTypeConfiguration<CommissionPriceParam>
{
    public void Configure(EntityTypeBuilder<CommissionPriceParam> builder)
    {
        builder.ToTable("INV_CommissionPriceParams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CustomerType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CommissionRate).HasPrecision(6, 3);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
