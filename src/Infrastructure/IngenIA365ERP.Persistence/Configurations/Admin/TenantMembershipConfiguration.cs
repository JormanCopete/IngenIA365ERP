using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Mapea <see cref="TenantMembership"/> a <c>ADM_TenantMemberships</c> alineado
/// con DDL 15b. Incluye:
/// <list type="bullet">
///   <item>UNIQUE (CentralUserId, TenantId) — una membresía por par.</item>
///   <item>Índices para listados por tenant y por user.</item>
///   <item>Índice filtrado <c>IsTenantAdmin=1 AND Status=Active</c> para la
///         salvaguarda del "último admin" (FR-040, research D-09).</item>
///   <item>HasQueryFilter para soft-delete (principio VII).</item>
/// </list>
/// </summary>
public class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("ADM_TenantMemberships");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CentralUserId).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.Status)
            .HasConversion<int>()
            .HasDefaultValue(MembershipStatus.Invited);
        builder.Property(e => e.IsTenantAdmin).HasDefaultValue(false);
        builder.Property(e => e.InvitedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // FK lógica al bridge CentralUserIdentity (mismo Guid Id que ADM_CentralUsers).
        // Sin navigation property en Domain — ver TenantMembership.CentralUserId XML doc.
        builder.HasIndex(e => new { e.CentralUserId, e.TenantId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_TenantMemberships_CentralUser_Tenant");

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_ADM_TenantMemberships_Tenant_Status");

        builder.HasIndex(e => new { e.CentralUserId, e.Status })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_ADM_TenantMemberships_CentralUser_Status");

        // Salvaguarda último admin: queries COUNT(*) WHERE TenantId=@t AND IsTenantAdmin=1 AND Status=Active
        builder.HasIndex(e => e.TenantId)
            .HasFilter("[IsTenantAdmin] = 1 AND [Status] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("IX_ADM_TenantMemberships_Tenant_ActiveAdmins");

        // Concurrencia optimista.
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
