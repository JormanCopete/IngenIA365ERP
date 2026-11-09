using IngenIA365ERP.Domain.Entities.Core.Connectors;
using IngenIA365ERP.Persistence.Configurations.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Connectors;

// Feature 020 (R15, R16, R18; data-model §3): los conectores de la cooperativa. La réplica (COR_Connectors y sus versiones) la escribe
// sólo el provisionador; las credenciales van cifradas y nunca salen (FR-057); el envío y sus intentos son lo que ve el tablero.

public class ConnectorConfiguration : IEntityTypeConfiguration<Connector>
{
    public void Configure(EntityTypeBuilder<Connector> builder)
    {
        builder.ComoEntidadDeTransporte("COR_Connectors");
        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(e => e.PlatformPublicId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_COR_Connectors_Platform");
    }
}

public class ConnectorVersionConfiguration : IEntityTypeConfiguration<ConnectorVersion>
{
    public void Configure(EntityTypeBuilder<ConnectorVersion> builder)
    {
        builder.ComoEntidadDeTransporte("COR_ConnectorVersions");
        builder.Property(e => e.BaseUrl).HasMaxLength(300).IsRequired();
        builder.Property(e => e.DefinitionJson).IsRequired();
        builder.Property(e => e.DefinitionSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasOne(e => e.Connector).WithMany(c => c.Versions).HasForeignKey(e => e.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ConnectorId, e.Number }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_COR_ConnectorVersions_Connector_Number");
    }
}

public class ConnectorCredentialConfiguration : IEntityTypeConfiguration<ConnectorCredential>
{
    public void Configure(EntityTypeBuilder<ConnectorCredential> builder)
    {
        builder.ComoEntidadDeTransporte("COR_ConnectorCredentials");
        builder.Property(e => e.ProtectedPayload).IsRequired();
        builder.Property(e => e.SetBy).HasMaxLength(150).IsRequired();
        builder.Property(e => e.LastVerificationMessage).HasMaxLength(500);
        builder.HasOne(e => e.Connector).WithMany().HasForeignKey(e => e.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.ConnectorId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_COR_ConnectorCredentials_Connector");
    }
}

public class ConnectorRequestConfiguration : IEntityTypeConfiguration<ConnectorRequest>
{
    public void Configure(EntityTypeBuilder<ConnectorRequest> builder)
    {
        builder.ComoEntidadDeTransporte("COR_ConnectorRequests");
        builder.Property(e => e.OriginRef).HasMaxLength(60).IsRequired();
        builder.Property(e => e.LastResponseExcerpt).HasMaxLength(2000);
        builder.Property(e => e.Warning).HasMaxLength(500);
        builder.Property(e => e.ResolvedBy).HasMaxLength(150);
        builder.Property(e => e.ResolutionReason).HasMaxLength(500);
        builder.HasOne(e => e.Connector).WithMany().HasForeignKey(e => e.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_COR_ConnectorRequests_Message");
        builder.HasIndex(e => e.OriginRef).HasDatabaseName("IX_COR_ConnectorRequests_OriginRef");
        builder.HasIndex(e => new { e.ConnectorId, e.Status }).HasDatabaseName("IX_COR_ConnectorRequests_Connector_Status");
    }
}

public class ConnectorCallConfiguration : IEntityTypeConfiguration<ConnectorCall>
{
    public void Configure(EntityTypeBuilder<ConnectorCall> builder)
    {
        builder.ComoEntidadDeTransporte("COR_ConnectorCalls");
        builder.Property(e => e.ResponseExcerpt).HasMaxLength(2048);
        builder.Property(e => e.RequestBodySanitized).HasMaxLength(4000);
        builder.Property(e => e.Url).HasMaxLength(400);
        builder.HasOne(e => e.Request).WithMany(r => r.Calls).HasForeignKey(e => e.RequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
