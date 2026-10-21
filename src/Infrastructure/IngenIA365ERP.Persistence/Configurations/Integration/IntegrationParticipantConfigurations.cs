using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Integration;

// Feature 013 (T0077; T16, C35, C36; data-model parte 3 §4.6): el registro de participantes de la integración. Las dos tablas
// llegan con la migración PlataformaParaCartera, que siembra CNT, INV, NOM habilitados y CAR inhabilitado. Los filtros se
// escriben con corchetes y ProviderModelConventions los traduce para PostgreSQL (comillas y FALSE).

/// <summary>
/// <c>COR_IntegrationParticipants</c>: único <c>(Code, Role, Destination)</c> entre las filas abiertas
/// (<c>UK_COR_IntegrationParticipants_Code_Role_Destination</c>, filtrado <c>[ValidTo] IS NULL AND [IsDeleted] = 0</c>; la historia
/// cerrada queda), con <c>Destination = ''</c> en un recaudador para que el único compare igual en los dos motores; y el índice de
/// los destinos (<c>IX_COR_IntegrationParticipants_Destination</c>, filtrado <c>[Role] = 2</c>).
/// </summary>
public class IntegrationParticipantConfiguration : IEntityTypeConfiguration<IntegrationParticipant>
{
    /// <summary>El único de las filas abiertas.</summary>
    public const string UnicoAbierto = "UK_COR_IntegrationParticipants_Code_Role_Destination";

    public void Configure(EntityTypeBuilder<IntegrationParticipant> builder)
    {
        builder.ToTable("COR_IntegrationParticipants");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_IntegrationParticipants_PublicId");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Class).HasConversion<int>().IsRequired();
        builder.Property(e => e.Role).HasConversion<int>().IsRequired();
        builder.Property(e => e.Destination).HasMaxLength(20).IsRequired();
        builder.Property(e => e.IsTestDouble).IsRequired();
        builder.Property(e => e.IsEnabled).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();
        builder.Property(e => e.RegisteredByKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.RegisteredByUserId);
        builder.Property(e => e.RegisteredByName).HasMaxLength(150).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Adoptions).WithOne(a => a.Participant).HasForeignKey(a => a.ParticipantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Code, e.Role, e.Destination }).IsUnique()
            .HasDatabaseName(UnicoAbierto).HasFilter("[ValidTo] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Destination, e.ValidFrom })
            .HasDatabaseName("IX_COR_IntegrationParticipants_Destination").HasFilter("[Role] = 2 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.RegisteredByUserId).HasDatabaseName("IX_COR_IntegrationParticipants_RegisteredByUser");

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>
/// <c>COR_IntegrationParticipantAdoptions</c>: único <c>(ParticipantId, ContractName, Version, ValidFrom)</c> entre las vivas
/// (<c>UK_COR_IntegrationParticipantAdoptions_Key</c>).
/// </summary>
public class IntegrationParticipantAdoptionConfiguration : IEntityTypeConfiguration<IntegrationParticipantAdoption>
{
    public void Configure(EntityTypeBuilder<IntegrationParticipantAdoption> builder)
    {
        builder.ToTable("COR_IntegrationParticipantAdoptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_IntegrationParticipantAdoptions_PublicId");

        builder.Property(e => e.ParticipantId).IsRequired();
        builder.Property(e => e.ContractName).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasIndex(e => new { e.ParticipantId, e.ContractName, e.Version, e.ValidFrom }).IsUnique()
            .HasDatabaseName("UK_COR_IntegrationParticipantAdoptions_Key").HasFilter("[IsDeleted] = 0");

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
