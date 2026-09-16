using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CreditLineParameterConfiguration : IEntityTypeConfiguration<CreditLineParameter>
{
    public void Configure(EntityTypeBuilder<CreditLineParameter> builder)
    {
        builder.ToTable("LND_CreditLineParameters");
        builder.Property(e => e.ProvisionExpenseAccountCode).HasMaxLength(12); // feature 009 (R16)
        builder.Property(e => e.ProvisionAccountCode).HasMaxLength(12);
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.HasIndex(e => e.CreditLineId).IsUnique();

        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(50);
        builder.Property(e => e.AllowExtension).HasMaxLength(1);
        builder.Property(e => e.DefaultInterestFlag).HasMaxLength(1);
        builder.Property(e => e.InterestType).HasMaxLength(5);
        builder.Property(e => e.GuaranteeClass).HasMaxLength(5);
        builder.Property(e => e.InstallmentType).HasMaxLength(5);
        builder.Property(e => e.AccountCode).HasMaxLength(20);
        builder.Property(e => e.GracePeriodFlag).HasMaxLength(1);
        builder.Property(e => e.Priority).HasMaxLength(5);
        builder.Property(e => e.GuarantorRequired).HasMaxLength(1);

        builder.Property(e => e.InterestRate).HasPrecision(10, 6);
        builder.Property(e => e.CreditLimit).HasPrecision(18, 2);
        builder.Property(e => e.ExtraRate).HasPrecision(10, 6);
        builder.Property(e => e.MaxAmount).HasPrecision(18, 2);
        builder.Property(e => e.AccrualRate).HasPrecision(10, 6);
        builder.Property(e => e.AdminRate).HasPrecision(10, 6);
        builder.Property(e => e.InsuranceRate).HasPrecision(10, 6);

        builder.HasMany(e => e.LoanPortfolios).WithOne(l => l.CreditLine).HasForeignKey(l => l.CreditLineId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
