using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

public class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        builder.ToTable("ADM_UserSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        // Una fila por usuario, ámbito y clave. Las tres columnas son NOT NULL
        // a propósito: ver el comentario de UserSetting.TenantPublicId sobre por
        // qué un NULL acá se comportaría distinto en cada motor.
        builder.HasIndex(e => new { e.CentralUserId, e.TenantPublicId, e.SettingKey })
            .IsUnique()
            .HasDatabaseName("UX_ADM_UserSettings_Usuario_Tenant_Clave");

        // La consulta habitual es "traeme todo lo de este usuario", de un golpe.
        builder.HasIndex(e => e.CentralUserId)
            .HasDatabaseName("IX_ADM_UserSettings_Usuario");

        builder.Property(e => e.SettingKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.SettingValue).HasMaxLength(4000);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
