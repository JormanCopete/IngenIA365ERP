using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_NotificationTemplates</c>. Feature 013 (T0084; T76; data-model parte 3 §4.12; C68): plantillas de mensaje versionadas con columnas
/// aditivas (con defecto, porque la tabla ya existe en cada cooperativa). Se retira <c>UK_COR_NotificationTemplates_Name_Channel</c> —sin
/// filtro, impediría versionar— y se crea <c>UK_COR_NotificationTemplates_Code_Channel_Version</c> filtrado a las vivas. Que dos versiones
/// del mismo (<c>Code</c>, <c>Channel</c>) no se crucen en el tiempo lo valida <c>CreateMessageTemplateVersionCommand</c>. Llegan con la
/// migración <c>PlataformaParaCartera</c>.
/// </summary>
public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public const string UnicoPorVersion = "UK_COR_NotificationTemplates_Code_Channel_Version";

    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("COR_NotificationTemplates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_NotificationTemplates_PublicId");

        builder.Property(e => e.TemplateName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Channel).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500);
        builder.Property(e => e.BodyTemplate).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        // Feature 013 (T0084): versionado.
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(e => e.Module).HasMaxLength(20).IsRequired().HasDefaultValue("Lending");
        builder.Property(e => e.Purpose).HasMaxLength(30).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(e => e.Version).IsRequired().HasDefaultValue(1);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.ProviderTemplateId).HasMaxLength(100);
        builder.Property(e => e.Variables).HasMaxLength(2000).IsRequired().HasDefaultValue("[]");
        builder.Property(e => e.IsSeeded).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.ReviewPending).IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => new { e.Code, e.Channel, e.Version }).IsUnique()
            .HasDatabaseName(UnicoPorVersion).HasFilter("[IsDeleted] = 0");

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
