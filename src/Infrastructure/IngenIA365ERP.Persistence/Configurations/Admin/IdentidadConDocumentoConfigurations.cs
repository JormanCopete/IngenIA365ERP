using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>Política de identidad por cooperativa (feature 017). Una fila por cooperativa, como la de MFA.</summary>
public class TenantIdentityPolicyConfiguration : IEntityTypeConfiguration<TenantIdentityPolicy>
{
    public void Configure(EntityTypeBuilder<TenantIdentityPolicy> builder)
    {
        builder.ToTable("ADM_TenantIdentityPolicies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.TenantId).IsRequired();
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_TenantIdentityPolicies_TenantId");

        builder.Property(e => e.AcceptedDocumentTypes).HasMaxLength(40).IsRequired()
            .HasDefaultValue(TenantIdentityPolicy.TiposPorDefecto);
        builder.Property(e => e.MaxActivationAttempts).HasDefaultValue(TenantIdentityPolicy.IntentosPorDefecto);

        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Historial de documentos (feature 017, FR-045): sólo inserción.</summary>
public class DocumentHistoryEntryConfiguration : IEntityTypeConfiguration<DocumentHistoryEntry>
{
    public void Configure(EntityTypeBuilder<DocumentHistoryEntry> builder)
    {
        builder.ToTable("ADM_DocumentHistory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CentralUserId).IsRequired();
        builder.Property(e => e.OldType).HasMaxLength(3);
        builder.Property(e => e.OldNumber).HasMaxLength(20);
        builder.Property(e => e.OldUsername).HasMaxLength(24);
        builder.Property(e => e.NewType).HasMaxLength(3).IsRequired();
        builder.Property(e => e.NewNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.NewUsername).HasMaxLength(24).IsRequired();
        builder.Property(e => e.Reason).HasConversion<int>();
        builder.Property(e => e.ActorKind).HasConversion<int>();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.SupportReference).HasMaxLength(200);

        builder.HasIndex(e => new { e.CentralUserId, e.OccurredAt })
            .HasDatabaseName("IX_ADM_DocumentHistory_CentralUser_OccurredAt");

        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Casos de identidad que resuelve el maestro (feature 017).</summary>
public class IdentityCaseConfiguration : IEntityTypeConfiguration<IdentityCase>
{
    public void Configure(EntityTypeBuilder<IdentityCase> builder)
    {
        builder.ToTable("ADM_IdentityCases");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Resolution).HasMaxLength(500);

        builder.HasIndex(e => new { e.Status, e.Kind })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_ADM_IdentityCases_Status_Kind");
        builder.HasIndex(e => e.CentralUserId).HasDatabaseName("IX_ADM_IdentityCases_CentralUser");

        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Solicitudes de cambio de correo (feature 017, D-09).</summary>
public class EmailChangeRequestConfiguration : IEntityTypeConfiguration<EmailChangeRequest>
{
    public void Configure(EntityTypeBuilder<EmailChangeRequest> builder)
    {
        builder.ToTable("ADM_EmailChangeRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.NewEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.NewNormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.CodeHash).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.RequestedFromIp).HasMaxLength(45);
        builder.Property(e => e.Reason).HasMaxLength(500);

        builder.HasIndex(e => new { e.CentralUserId, e.Status })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_ADM_EmailChangeRequests_CentralUser_Status");

        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
