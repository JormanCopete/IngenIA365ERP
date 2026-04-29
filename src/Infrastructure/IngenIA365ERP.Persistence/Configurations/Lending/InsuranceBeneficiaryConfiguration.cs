using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class InsuranceBeneficiaryConfiguration : IEntityTypeConfiguration<InsuranceBeneficiary>
{
    public void Configure(EntityTypeBuilder<InsuranceBeneficiary> builder)
    {
        builder.ToTable("LND_InsuranceBeneficiaries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_InsuranceBeneficiaries_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BeneficiaryId).HasMaxLength(15).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(60).IsRequired();
        builder.Property(e => e.InsurancePercentage).HasPrecision(6, 3);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
