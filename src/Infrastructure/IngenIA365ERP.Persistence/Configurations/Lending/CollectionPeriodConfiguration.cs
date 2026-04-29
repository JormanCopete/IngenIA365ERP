using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CollectionPeriodConfiguration : IEntityTypeConfiguration<CollectionPeriod>
{
    public void Configure(EntityTypeBuilder<CollectionPeriod> builder)
    {
        builder.ToTable("LND_CollectionPeriods");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CollectionPeriods_PublicId");

        builder.Property(e => e.UserId).HasMaxLength(15).IsRequired();
        builder.Property(e => e.LastPersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DeductionClass).HasMaxLength(2).IsRequired();
        builder.Property(e => e.SortOrder).HasMaxLength(2).IsRequired();
        builder.Property(e => e.LegalCollection).HasMaxLength(2).IsRequired();


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
