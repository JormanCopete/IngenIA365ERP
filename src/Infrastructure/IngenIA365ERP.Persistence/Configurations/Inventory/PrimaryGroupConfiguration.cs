using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class PrimaryGroupConfiguration : IEntityTypeConfiguration<PrimaryGroup>
{
    public void Configure(EntityTypeBuilder<PrimaryGroup> builder)
    {
        builder.ToTable("INV_PrimaryGroups");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.GroupCode).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(50);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
