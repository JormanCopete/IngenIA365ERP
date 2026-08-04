using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class SecondaryGroupConfiguration : IEntityTypeConfiguration<SecondaryGroup>
{
    public void Configure(EntityTypeBuilder<SecondaryGroup> builder)
    {
        builder.ToTable("INV_SecondaryGroups");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.GroupCode).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(50);

        builder.HasOne(e => e.PrimaryGroup).WithMany(p => p.SecondaryGroups).HasForeignKey(e => e.PrimaryGroupId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
