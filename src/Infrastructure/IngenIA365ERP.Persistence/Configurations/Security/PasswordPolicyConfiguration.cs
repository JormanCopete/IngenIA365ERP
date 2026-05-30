using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class PasswordPolicyConfiguration : IEntityTypeConfiguration<PasswordPolicy>
{
    public void Configure(EntityTypeBuilder<PasswordPolicy> builder)
    {
        builder.ToTable("SEC_PasswordPolicies");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.MinLength).HasDefaultValue(12);
        builder.Property(e => e.ExpiryDays).HasDefaultValue(90);
        builder.Property(e => e.HistorySize).HasDefaultValue(5);
        builder.Property(e => e.LockoutThreshold).HasDefaultValue(5);
        builder.Property(e => e.LockoutMinutes).HasDefaultValue(15);

        // Una política activa por tenant (filtered index: ignora soft-deleted).
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_SEC_PasswordPolicies_TenantId_NotDeleted");

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
