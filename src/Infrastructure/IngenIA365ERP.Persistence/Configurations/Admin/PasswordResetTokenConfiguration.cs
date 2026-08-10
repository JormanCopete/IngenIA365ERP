using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// T079h — Mapeo EF de <see cref="PasswordResetToken"/> contra
/// <c>ADM_PasswordResetTokens</c>.
/// </summary>
public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("ADM_PasswordResetTokens");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CentralUserId).IsRequired();

        builder.Property(e => e.TokenHash)
            .HasColumnType("binary(32)")
            .IsRequired();
        builder.HasIndex(e => e.TokenHash).IsUnique();

        builder.Property(e => e.RequesterIp).HasMaxLength(45);

        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.ConsumedAt);

        // Auditoría (AuditableEntity).
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // RowVersion (BaseEntity).
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Soft-delete filter (BaseEntity).
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Índice para resolver "token activo del usuario" sin escanear toda la tabla.
        builder.HasIndex(e => new { e.CentralUserId, e.ExpiresAt })
            .HasDatabaseName("IX_ADM_PasswordResetTokens_CentralUserId_ExpiresAt")
            .HasFilter("[ConsumedAt] IS NULL AND [IsDeleted] = 0");
    }
}
