using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class SalesPointConfiguration : IEntityTypeConfiguration<SalesPoint>
{
    public void Configure(EntityTypeBuilder<SalesPoint> builder)
    {
        builder.ToTable("INV_SalesPoints");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.PointCode, e.Status, e.DateId }).IsUnique();

        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.PrinterName).HasMaxLength(50);
        builder.Property(e => e.BaseAmount).HasPrecision(18, 2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
