using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;
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

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique()
            .HasDatabaseName("UK_CMP_HabeasPolicyVersions_PublicId");

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.ContentMarkdown).IsRequired();
        builder.Property(e => e.Sha256Hex).HasMaxLength(64).IsRequired();
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.PublishedBy).HasMaxLength(100).IsRequired();
        // Feature 013 (T69 a, C68, T0086): la finalidad, como texto; las versiones existentes quedan en GeneralTreatment.
        builder.Property(e => e.Purpose).HasConversion<string>().HasMaxLength(30).IsRequired()
            .HasDefaultValue(DataAuthorizationPurpose.GeneralTreatment).HasSentinel((DataAuthorizationPurpose)0);

        // VersionNumber único por tenant y finalidad (C68: se rehace con Purpose).
        builder.HasIndex(e => new { e.TenantId, e.Purpose, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_CMP_HabeasPolicyVersions_TenantVersion");

        // A lo sumo UNA versión vigente por tenant y finalidad (EffectiveTo IS NULL).
        builder.HasIndex(e => new { e.TenantId, e.Purpose })
            .IsUnique()
            .HasFilter("[EffectiveTo] IS NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UK_CMP_HabeasPolicyVersions_Current");

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
