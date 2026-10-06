using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_ExternalChannelSettings</c> (feature 013, T0080; T72; data-model parte 3 §4.7): el canal externo por cooperativa y clase, con
/// sólo la referencia a la credencial. Único <c>(SettingKey, ValidFrom)</c> entre las vivas
/// (<c>UK_COR_ExternalChannelSettings_Key_ValidFrom</c>); que dos vigencias de la misma clave no se cruzan lo valida el comando
/// (<c>Core.ExternalChannel.Overlaps</c>). Llega con la migración <c>PlataformaParaCartera</c>. (nuevo)
/// </summary>
public class ExternalChannelSettingConfiguration : IEntityTypeConfiguration<ExternalChannelSetting>
{
    public const string UnicoPorVigencia = "UK_COR_ExternalChannelSettings_Key_ValidFrom";

    public void Configure(EntityTypeBuilder<ExternalChannelSetting> builder)
    {
        builder.ToTable("COR_ExternalChannelSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_ExternalChannelSettings_PublicId");

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.BureauCode).HasMaxLength(20);
        builder.Property(e => e.SettingKey).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ChannelCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.CredentialKey).HasMaxLength(100);
        builder.Property(e => e.VerifiedAt);
        builder.Property(e => e.VerifiedByUserId);
        builder.Property(e => e.VerifiedByName).HasMaxLength(150);
        builder.Property(e => e.LastVerificationOutcome).HasConversion<int?>();
        builder.Property(e => e.LastVerificationMessage).HasMaxLength(500);
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SettingKey, e.ValidFrom }).IsUnique()
            .HasDatabaseName(UnicoPorVigencia).HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.VerifiedByUserId).HasDatabaseName("IX_COR_ExternalChannelSettings_VerifiedByUser");

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
