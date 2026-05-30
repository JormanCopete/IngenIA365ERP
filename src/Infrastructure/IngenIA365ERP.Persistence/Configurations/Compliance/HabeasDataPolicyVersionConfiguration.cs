using IngenIA365ERP.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Compliance;

public class HabeasDataPolicyVersionConfiguration
    : IEntityTypeConfiguration<HabeasDataPolicyVersion>
{
    public void Configure(EntityTypeBuilder<HabeasDataPolicyVersion> builder)
    {
        builder.ToTable("CMP_HabeasDataPolicyVersions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique()
            .HasDatabaseName("UK_CMP_HabeasPolicyVersions_PublicId");

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.ContentMarkdown).IsRequired();
        builder.Property(e => e.Sha256Hex).HasMaxLength(64).IsRequired();
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.PublishedBy).HasMaxLength(100).IsRequired();

        // VersionNumber único por tenant.
        builder.HasIndex(e => new { e.TenantId, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_CMP_HabeasPolicyVersions_TenantVersion");

        // A lo sumo UNA versión vigente por tenant (EffectiveTo IS NULL).
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasFilter("[EffectiveTo] IS NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UK_CMP_HabeasPolicyVersions_Current");

        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
