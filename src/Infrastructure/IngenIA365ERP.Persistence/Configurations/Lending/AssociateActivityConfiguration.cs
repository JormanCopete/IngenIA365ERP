using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class AssociateActivityConfiguration : IEntityTypeConfiguration<AssociateActivity>
{
    public void Configure(EntityTypeBuilder<AssociateActivity> builder)
    {
        builder.ToTable("LND_AssociateActivities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_AssociateActivities_PublicId");

        builder.Property(e => e.ActivityType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ActivityCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BeneficiaryId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Remarks).HasMaxLength(120);
        builder.Property(e => e.Attended).HasMaxLength(2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
