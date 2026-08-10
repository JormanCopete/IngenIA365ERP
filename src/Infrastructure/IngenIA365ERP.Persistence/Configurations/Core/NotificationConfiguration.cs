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

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Notifications_PublicId");

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.RecipientUserPublicId).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.ChannelsMask).IsRequired();
        builder.Property(e => e.EmailStatus).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");

        // Lookup patrón "inbox del usuario X": (TenantId, RecipientUserPublicId, ReadAt).
        builder.HasIndex(e => new { e.TenantId, e.RecipientUserPublicId, e.ReadAt })
            .HasDatabaseName("IX_COR_Notifications_Inbox")
            .HasFilter("[IsDeleted] = 0");

        // Cola del dispatcher: pendientes por email.
        builder.HasIndex(e => e.EmailStatus)
            .HasDatabaseName("IX_COR_Notifications_EmailStatus")
            .HasFilter("[IsDeleted] = 0");

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
