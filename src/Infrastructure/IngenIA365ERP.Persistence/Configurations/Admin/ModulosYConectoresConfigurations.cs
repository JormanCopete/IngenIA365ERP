using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

// Feature 020 (R1, R15; data-model §1): módulos por cooperativa y conectores de la plataforma, en la base administrativa.

/// <summary><c>ADM_TenantModules</c>: un módulo habilitable por cooperativa.</summary>
public class TenantModuleConfiguration : IEntityTypeConfiguration<TenantModule>
{
    public void Configure(EntityTypeBuilder<TenantModule> builder)
    {
        builder.ToTable("ADM_TenantModules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ModuleCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.ChangedBy).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.ModuleCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_TenantModules_Tenant_Module");

        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary><c>ADM_Connectors</c>: un conector externo definido por el maestro.</summary>
public class PlatformConnectorConfiguration : IEntityTypeConfiguration<PlatformConnector>
{
    public void Configure(EntityTypeBuilder<PlatformConnector> builder)
    {
        builder.ToTable("ADM_Connectors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_ADM_Connectors_Code");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary><c>ADM_ConnectorVersions</c>: una versión publicada, hecho inmutable.</summary>
public class PlatformConnectorVersionConfiguration : IEntityTypeConfiguration<PlatformConnectorVersion>
{
    public void Configure(EntityTypeBuilder<PlatformConnectorVersion> builder)
    {
        builder.ToTable("ADM_ConnectorVersions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.Property(e => e.BaseUrl).HasMaxLength(300).IsRequired();
        builder.Property(e => e.DefinitionJson).IsRequired();
        builder.Property(e => e.DefinitionSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(e => e.PublishedBy).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasOne(e => e.Connector).WithMany(c => c.Versions).HasForeignKey(e => e.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ConnectorId, e.Number }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_ConnectorVersions_Connector_Number");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary><c>ADM_TenantConnectors</c>: la habilitación de un conector por cooperativa.</summary>
public class TenantConnectorConfiguration : IEntityTypeConfiguration<TenantConnector>
{
    public void Configure(EntityTypeBuilder<TenantConnector> builder)
    {
        builder.ToTable("ADM_TenantConnectors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ChangedBy).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.HasOne(e => e.Connector).WithMany().HasForeignKey(e => e.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TenantId, e.ConnectorId }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_TenantConnectors_Tenant_Connector");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
