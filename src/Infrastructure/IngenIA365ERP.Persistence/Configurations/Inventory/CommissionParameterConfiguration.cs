using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class CommissionParameterConfiguration : IEntityTypeConfiguration<CommissionParameter>
{
    public void Configure(EntityTypeBuilder<CommissionParameter> builder)
    {
        builder.ToTable("INV_CommissionParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.SalesRangeStart).HasPrecision(18, 2);
        builder.Property(e => e.SalesRangeEnd).HasPrecision(18, 2);
        builder.Property(e => e.CommissionRate).HasPrecision(6, 3);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
