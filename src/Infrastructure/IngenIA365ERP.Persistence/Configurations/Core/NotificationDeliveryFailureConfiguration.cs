using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class NotificationDeliveryFailureConfiguration
    : IEntityTypeConfiguration<NotificationDeliveryFailure>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryFailure> builder)
    {
        builder.ToTable("COR_NotificationDeliveryFailures");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique()
            .HasDatabaseName("UK_COR_NotDeliveryFailures_PublicId");

        builder.Property(e => e.NotificationId).IsRequired();
        builder.Property(e => e.Channel).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AttemptNumber).IsRequired();
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.FailedAt).IsRequired();

        builder.HasIndex(e => e.NotificationId)
            .HasDatabaseName("IX_COR_NotDeliveryFailures_NotificationId");

        builder.HasOne(e => e.Notification).WithMany()
            .HasForeignKey(e => e.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
