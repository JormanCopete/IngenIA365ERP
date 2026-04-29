using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("COR_Notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Notifications_PublicId");

        builder.Property(e => e.Channel).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        // FK indexes
        builder.HasIndex(e => e.TemplateId).HasDatabaseName("IX_COR_Notifications_TemplateId");
        builder.HasIndex(e => e.RecipientPersonId).HasDatabaseName("IX_COR_Notifications_RecipientPersonId");

        // Relationships
        builder.HasOne(e => e.Template).WithMany(t => t.Notifications).HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
        // RecipientPerson relationship configured from PersonConfiguration

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
