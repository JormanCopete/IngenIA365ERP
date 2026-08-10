using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class SubZoneConfiguration : IEntityTypeConfiguration<SubZone>
{
    public void Configure(EntityTypeBuilder<SubZone> builder)
    {
        builder.ToTable("LND_SubZones");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_SubZones_PublicId");

        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(120).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_SubZones_Code");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
