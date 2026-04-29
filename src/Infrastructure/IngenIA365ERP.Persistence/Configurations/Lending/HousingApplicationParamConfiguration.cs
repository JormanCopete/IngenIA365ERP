using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class HousingApplicationParamConfiguration : IEntityTypeConfiguration<HousingApplicationParam>
{
    public void Configure(EntityTypeBuilder<HousingApplicationParam> builder)
    {
        builder.ToTable("LND_HousingApplicationParams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_HousingApplicationParams_PublicId");

        builder.Property(e => e.SocialInterest).HasMaxLength(2).IsRequired();
        builder.Property(e => e.HasSubsidy).HasMaxLength(2).IsRequired();
        builder.HasIndex(e => e.ApplicationNumber).IsUnique().HasDatabaseName("UK_LND_HousingAppParams_App");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
