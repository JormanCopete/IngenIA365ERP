using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("ADM_Subscriptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.PlanName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(5).HasDefaultValue("COP");
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Active");
        builder.Property(e => e.PaymentMethod).HasMaxLength(50);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
