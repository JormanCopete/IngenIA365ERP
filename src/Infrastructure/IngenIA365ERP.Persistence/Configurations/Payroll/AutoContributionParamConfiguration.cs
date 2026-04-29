using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class AutoContributionParamConfiguration : IEntityTypeConfiguration<AutoContributionParam>
{
    public void Configure(EntityTypeBuilder<AutoContributionParam> builder)
    {
        builder.ToTable("PAY_AutoContributionParams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Fax).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CityName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DepartmentName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.HealthRate).HasPrecision(6, 3);
        builder.Property(e => e.PensionRate).HasPrecision(6, 3);
        builder.Property(e => e.WorkRiskRate).HasPrecision(6, 3);
        builder.Property(e => e.CcfRate).HasPrecision(6, 3);
        builder.Property(e => e.SenaRate).HasPrecision(6, 3);
        builder.Property(e => e.IcbfRate).HasPrecision(6, 3);
        builder.Property(e => e.SolidarityFundRate).HasPrecision(6, 3);
        builder.Property(e => e.EsapRate).HasPrecision(6, 3);
        builder.Property(e => e.EducationMinRate).HasPrecision(6, 3);
        builder.Property(e => e.LatePaymentRate).HasPrecision(6, 3);
        builder.Property(e => e.MinimumWage).HasPrecision(18, 2);
        builder.Property(e => e.CcfAdminCode).HasMaxLength(6).IsRequired();
        builder.Property(e => e.WorkRiskAdminCode).HasMaxLength(6).IsRequired();
        builder.Property(e => e.EmployerNumber).HasMaxLength(15).IsRequired();
        builder.Property(e => e.FormNumber).HasMaxLength(15).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RepresentativeId).HasMaxLength(15).IsRequired();
        builder.Property(e => e.RepresentativeCheckDigit).HasMaxLength(1).IsRequired();
        builder.Property(e => e.RepresentativeLastName1).HasMaxLength(30).IsRequired();
        builder.Property(e => e.RepresentativeLastName2).HasMaxLength(30).IsRequired();
        builder.Property(e => e.RepresentativeFirstName1).HasMaxLength(30).IsRequired();
        builder.Property(e => e.RepresentativeFirstName2).HasMaxLength(30).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
