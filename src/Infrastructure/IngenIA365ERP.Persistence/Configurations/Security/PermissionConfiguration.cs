using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("SEC_Permissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.Resource, e.Action }).IsUnique();

        builder.Property(e => e.Resource).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
