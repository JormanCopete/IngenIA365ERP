using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class MinuteAttendeeConfiguration : IEntityTypeConfiguration<MinuteAttendee>
{
    public void Configure(EntityTypeBuilder<MinuteAttendee> builder)
    {
        builder.ToTable("LND_MinuteAttendees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_MinuteAttendees_PublicId");

        builder.Property(e => e.MinutesType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.MinutesNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
