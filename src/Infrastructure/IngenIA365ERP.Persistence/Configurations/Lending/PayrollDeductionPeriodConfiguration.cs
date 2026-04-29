using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PayrollDeductionPeriodConfiguration : IEntityTypeConfiguration<PayrollDeductionPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollDeductionPeriod> builder)
    {
        builder.ToTable("LND_PayrollDeductionPeriods");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PayrollDeductionPeriods_PublicId");

        builder.Property(e => e.CompanyCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.BranchId).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Periodicity).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IsAdditional).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DeductionClass).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PaymentCycle).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ArrearsExtras).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ContributionAmount).HasPrecision(18, 2);
        builder.Property(e => e.LoanAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestAmount).HasPrecision(18, 2);
        builder.Property(e => e.ExtraAmount).HasPrecision(18, 2);
        builder.Property(e => e.DefaultAmount).HasPrecision(18, 2);
        builder.Property(e => e.InsuranceAmount).HasPrecision(18, 2);
        builder.Property(e => e.AdminAmount).HasPrecision(18, 2);
        builder.Property(e => e.OtherAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
