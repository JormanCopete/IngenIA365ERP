using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Mapea <see cref="Invitation"/> a <c>ADM_Invitations</c> alineado con DDL 15b.
/// Single-use estricto vía UNIQUE TokenHash + RowVersion (research D-06).
/// </summary>
public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("ADM_Invitations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.InvitedByUserId).IsRequired();
        builder.Property(e => e.InviteAsTenantAdmin).HasDefaultValue(false);

        builder.Property(e => e.TokenHash)
            .HasColumnType($"binary({InvitationToken.HashBytes})")
            .IsRequired();
        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_ADM_Invitations_TokenHash");

        builder.Property(e => e.Status)
            .HasConversion<int>()
            .HasDefaultValue(InvitationStatus.Pending);

        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.CreatedAt);

        builder.HasIndex(e => new { e.NormalizedEmail, e.TenantId, e.Status })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_ADM_Invitations_Email_Tenant_Status");

        // Job de expiración (T122).
        builder.HasIndex(e => new { e.Status, e.ExpiresAt })
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0")
            .HasDatabaseName("IX_ADM_Invitations_Status_ExpiresAt");

        // Listado del admin de empresa.
        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_ADM_Invitations_Tenant_Status");

        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
