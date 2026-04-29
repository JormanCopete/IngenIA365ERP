using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PayrollDeductionConfiguration : IEntityTypeConfiguration<PayrollDeduction>
{
    public void Configure(EntityTypeBuilder<PayrollDeduction> builder)
    {
        builder.ToTable("LND_PayrollDeductions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PayrollDeductions_PublicId");

        builder.Property(e => e.CompanyCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.BranchId).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Periodicity).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IsAdditional).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(50).IsRequired();
        builder.Property(e => e.PaymentCycle).HasMaxLength(2).IsRequired();
        builder.Property(e => e.CodeudorCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ContributionAmount).HasPrecision(18, 2);
        builder.Property(e => e.LoanAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestAmount).HasPrecision(18, 2);
        builder.Property(e => e.ExtraAmount).HasPrecision(18, 2);
        builder.Property(e => e.DefaultAmount).HasPrecision(18, 2);
        builder.Property(e => e.InsuranceAmount).HasPrecision(18, 2);
        builder.Property(e => e.AdminAmount).HasPrecision(18, 2);
        builder.Property(e => e.OtherAmount).HasPrecision(18, 2);
        builder.Property(e => e.ContributionApplied).HasPrecision(18, 2);
        builder.Property(e => e.LoanApplied).HasPrecision(18, 2);
        builder.Property(e => e.InterestApplied).HasPrecision(18, 2);
        builder.Property(e => e.ExtraApplied).HasPrecision(18, 2);
        builder.Property(e => e.DefaultApplied).HasPrecision(18, 2);
        builder.Property(e => e.InsuranceApplied).HasPrecision(18, 2);
        builder.Property(e => e.AdminApplied).HasPrecision(18, 2);
        builder.Property(e => e.OtherApplied).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
