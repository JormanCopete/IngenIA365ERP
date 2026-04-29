using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class LoanPortfolioConfiguration : IEntityTypeConfiguration<LoanPortfolio>
{
    public void Configure(EntityTypeBuilder<LoanPortfolio> builder)
    {
        builder.ToTable("LND_LoanPortfolios");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.IdentificationNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => new { e.PersonId, e.CreditLineId, e.PortfolioNumber }).IsUnique();

        builder.Property(e => e.RequestedAmount).HasPrecision(18, 2);
        builder.Property(e => e.ApprovedAmount).HasPrecision(18, 2);
        builder.Property(e => e.CurrentBalance).HasPrecision(18, 2);
        builder.Property(e => e.InstallmentAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestRate).HasPrecision(10, 6);
        builder.Property(e => e.AdminFeeRate).HasPrecision(10, 6);
        builder.Property(e => e.InsuranceRate).HasPrecision(10, 6);
        builder.Property(e => e.CapitalBalanceCurrent).HasPrecision(18, 2);
        builder.Property(e => e.InterestBalanceCurrent).HasPrecision(18, 2);
        builder.Property(e => e.DefaultBalanceCurrent).HasPrecision(18, 2);
        builder.Property(e => e.AdminBalanceCurrent).HasPrecision(18, 2);
        builder.Property(e => e.InsuranceBalanceCurrent).HasPrecision(18, 2);
        builder.Property(e => e.CapitalAccrued).HasPrecision(18, 2);
        builder.Property(e => e.InterestAccrued).HasPrecision(18, 2);
        builder.Property(e => e.InsuranceAccrued).HasPrecision(18, 2);
        builder.Property(e => e.AdminAccrued).HasPrecision(18, 2);
        builder.Property(e => e.DefaultInterest).HasPrecision(18, 2);
        builder.Property(e => e.ProvisionRate).HasPrecision(10, 4);
        builder.Property(e => e.ProvisionAmount).HasPrecision(18, 2);

        builder.Property(e => e.PaymentCycle).HasMaxLength(5);
        builder.Property(e => e.PaymentPeriodicity).HasMaxLength(5);
        builder.Property(e => e.InstallmentType).HasMaxLength(5);
        builder.Property(e => e.InterestType).HasMaxLength(5);
        builder.Property(e => e.GuaranteeType).HasMaxLength(5);
        builder.Property(e => e.DeductionType).HasMaxLength(5);
        builder.Property(e => e.Category).HasMaxLength(5);
        builder.Property(e => e.LegalCollection).HasMaxLength(5);

        builder.HasIndex(e => e.PersonId);

        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId);
        builder.HasOne(e => e.CreditLine).WithMany(c => c.LoanPortfolios).HasForeignKey(e => e.CreditLineId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
