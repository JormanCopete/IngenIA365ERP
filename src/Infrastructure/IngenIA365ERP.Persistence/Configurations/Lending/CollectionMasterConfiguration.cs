using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CollectionMasterConfiguration : IEntityTypeConfiguration<CollectionMaster>
{
    public void Configure(EntityTypeBuilder<CollectionMaster> builder)
    {
        builder.ToTable("LND_CollectionMasters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CollectionMasters_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.TotalBalance).HasPrecision(18, 2);
        builder.Property(e => e.OverdueCapital).HasPrecision(18, 2);
        builder.Property(e => e.OverdueInterest).HasPrecision(18, 2);
        builder.Property(e => e.InstallmentAmount).HasPrecision(18, 2);
        builder.Property(e => e.DaysOverdue).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
