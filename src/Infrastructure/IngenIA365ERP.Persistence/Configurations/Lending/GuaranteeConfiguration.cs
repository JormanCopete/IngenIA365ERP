using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class GuaranteeConfiguration : IEntityTypeConfiguration<Guarantee>
{
    public void Configure(EntityTypeBuilder<Guarantee> builder)
    {
        builder.ToTable("LND_Guarantees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_Guarantees_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RegistrationNumber).HasMaxLength(25).IsRequired();
        builder.Property(e => e.GuaranteeType).HasMaxLength(3).IsRequired();
        builder.Property(e => e.HasInsurance).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PolicyNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InsurerIdentification).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InsurerName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.GuaranteeStatus).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Guarantor1).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Guarantor2).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Guarantor3).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Guarantor4).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LegacyCodigoTer).HasMaxLength(20);
        builder.Property(e => e.CadastralAppraisal).HasPrecision(18, 2);
        builder.Property(e => e.CommercialAppraisal).HasPrecision(18, 2);
        builder.Property(e => e.InsuredPercentage).HasPrecision(5, 2);
        builder.Property(e => e.Balance).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
