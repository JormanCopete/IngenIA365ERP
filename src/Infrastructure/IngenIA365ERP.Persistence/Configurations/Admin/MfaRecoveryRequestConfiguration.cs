using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Mapea <see cref="MfaRecoveryRequest"/> a <c>ADM_MfaRecoveryRequests</c>.
/// </summary>
public class MfaRecoveryRequestConfiguration : IEntityTypeConfiguration<MfaRecoveryRequest>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryRequest> builder)
    {
        builder.ToTable("ADM_MfaRecoveryRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CentralUserId).IsRequired();

        // Los dos hashes son SHA-256: 32 bytes exactos, no varbinary(max). Y los dos
        // llevan índice único porque son la única forma de resolver un enlace, y sin
        // el índice cada clic recorrería la tabla entera.
        builder.Property(e => e.TokenHash).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_MfaRecoveryRequests_TokenHash");

        builder.Property(e => e.CancelTokenHash).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => e.CancelTokenHash)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_MfaRecoveryRequests_CancelTokenHash");

        // «Las vivas de esta persona» — se consulta al pedir una nueva y en cada
        // ingreso correcto, así que va en el camino caliente del login.
        builder.HasIndex(e => e.CentralUserId)
            .HasDatabaseName("IX_ADM_MfaRecoveryRequests_CentralUserId")
            .HasFilter("[IsDeleted] = 0");

        builder.Property(e => e.MotivoDeCancelacion).HasMaxLength(64);
        builder.Property(e => e.IpSolicitante).HasMaxLength(45);

        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Las solicitudes NO se borran al ejecutarse ni al cancelarse: se marcan.
        // Son el rastro de quién intentó retirarle el segundo factor a quién, y ese
        // rastro es la mitad del valor de este diseño — el día que haya que
        // reconstruir un incidente, «se canceló» tiene que poder distinguirse de
        // «nunca existió».
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
