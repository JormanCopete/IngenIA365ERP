using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class ProvisionParameterConfiguration : IEntityTypeConfiguration<ProvisionParameter>
{
    public void Configure(EntityTypeBuilder<ProvisionParameter> builder)
    {
        builder.ToTable("LND_ProvisionParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_ProvisionParameters_PublicId");

        builder.Property(e => e.RateB).HasPrecision(6, 3);
        builder.Property(e => e.RateC).HasPrecision(6, 3);
        builder.Property(e => e.RateD).HasPrecision(6, 3);
        builder.Property(e => e.RateE).HasPrecision(6, 3);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
