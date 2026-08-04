using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
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

        // Unique: TemplateName + Channel
        builder.HasIndex(e => new { e.TemplateName, e.Channel }).IsUnique().HasDatabaseName("UK_COR_NotificationTemplates_Name_Channel");

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
