using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    public void Configure(EntityTypeBuilder<LoanApplication> builder)
    {
        builder.ToTable("LND_LoanApplications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_LoanApplications_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EmployerName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Position).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ContractType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.GuaranteeType).HasMaxLength(3).IsRequired();
        builder.Property(e => e.IsInsured).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.IdentificationNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PaymentCycle).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Periodicity).HasMaxLength(2).IsRequired();
        builder.Property(e => e.BranchId).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.PaymasterDeductionType).HasMaxLength(2);
        builder.Property(e => e.RequestedAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestRate).HasPrecision(7, 6);
        builder.Property(e => e.InstallmentAmount).HasPrecision(18, 2);
        builder.Property(e => e.ApprovedAmount).HasPrecision(18, 2);
        builder.Property(e => e.Salary).HasPrecision(18, 2);
        builder.Property(e => e.OtherIncome).HasPrecision(18, 2);
        builder.Property(e => e.AdminRate).HasPrecision(12, 5);
        builder.Property(e => e.InsuranceRate).HasPrecision(10, 5);
        builder.Property(e => e.DtfRate).HasPrecision(10, 5);
        builder.Property(e => e.SpreadPoints).HasPrecision(10, 5);
        builder.HasIndex(e => e.ApplicationNumber).IsUnique().HasDatabaseName("UK_LND_LoanApplications_Number");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
