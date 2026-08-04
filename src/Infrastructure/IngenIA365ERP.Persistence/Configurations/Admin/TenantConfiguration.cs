using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("ADM_Tenants");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.SchemaName).IsUnique();
        builder.HasIndex(e => e.Subdomain).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SchemaName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Subdomain).HasMaxLength(100);
        builder.Property(e => e.PlanType).HasMaxLength(50).HasDefaultValue("Basic");
        builder.Property(e => e.DatabaseName).HasMaxLength(100);
        builder.Property(e => e.ContactEmail).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ContactPhone).HasMaxLength(30);

        builder.HasMany(e => e.Subscriptions).WithOne(s => s.Tenant).HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Settings).WithOne(s => s.Tenant).HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
