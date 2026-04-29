using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("INV_Shifts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.ShiftCode).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.StartTime).HasMaxLength(10);
        builder.Property(e => e.EndTime).HasMaxLength(10);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
