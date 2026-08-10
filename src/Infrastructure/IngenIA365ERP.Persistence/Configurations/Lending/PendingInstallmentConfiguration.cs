using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PendingInstallmentConfiguration : IEntityTypeConfiguration<PendingInstallment>
{
    public void Configure(EntityTypeBuilder<PendingInstallment> builder)
    {
        builder.ToTable("LND_PendingInstallments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PendingInstallments_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.CompanyCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Periodicity).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DeductionType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IsAdvancePayment).HasMaxLength(2).IsRequired();
        builder.Property(e => e.EntryType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ExtraPaymentForm).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DeductionClass).HasMaxLength(2).IsRequired();
        builder.Property(e => e.GuaranteeClass).HasMaxLength(3).IsRequired();
        builder.Property(e => e.EntryReason).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AccruedAll).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AccruedNoExtra).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AccruedOnlyExtra).HasMaxLength(2).IsRequired();
        builder.Property(e => e.LegacyCodigoTer).HasMaxLength(20);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);
        builder.Property(e => e.AccruedInterest).HasPrecision(18, 2);
        builder.Property(e => e.AccruedCapital).HasPrecision(18, 2);
        builder.Property(e => e.AccruedExtra).HasPrecision(18, 2);
        builder.Property(e => e.PaidInterest).HasPrecision(18, 2);
        builder.Property(e => e.PaidCapital).HasPrecision(18, 2);
        builder.Property(e => e.PaidExtra).HasPrecision(18, 2);
        builder.Property(e => e.BalanceInterest).HasPrecision(18, 2);
        builder.Property(e => e.BalanceCapital).HasPrecision(18, 2);
        builder.Property(e => e.BalanceExtra).HasPrecision(18, 2);
        builder.Property(e => e.CreditBalance).HasPrecision(18, 2);
        builder.Property(e => e.ObligationInstallment).HasPrecision(18, 2);
        builder.Property(e => e.TotalInstallment).HasPrecision(18, 2);
        builder.Property(e => e.DefaultInterest).HasPrecision(18, 2);
        builder.Property(e => e.DefaultInterestBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorCapitalBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorInterestBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorExtraBalance).HasPrecision(18, 2);
        builder.Property(e => e.DefaultInterestAccrued).HasPrecision(18, 2);
        builder.Property(e => e.DefaultInterestPaid).HasPrecision(18, 2);
        builder.Property(e => e.AdvanceAmount).HasPrecision(18, 2);
        builder.Property(e => e.AdvanceCapital).HasPrecision(18, 2);
        builder.Property(e => e.AdvanceInterest).HasPrecision(18, 2);
        builder.Property(e => e.AdvanceExtra).HasPrecision(18, 2);
        builder.Property(e => e.TotalBalance).HasPrecision(18, 2);
        builder.Property(e => e.DaysOverdue).HasPrecision(18, 2);
        builder.Property(e => e.InterestRate).HasPrecision(7, 4);
        builder.Property(e => e.PriorInsuranceBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorAdminBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorOtherBalance).HasPrecision(18, 2);
        builder.Property(e => e.AccruedInsurance).HasPrecision(18, 2);
        builder.Property(e => e.AccruedAdmin).HasPrecision(18, 2);
        builder.Property(e => e.AccruedOther).HasPrecision(18, 2);
        builder.Property(e => e.PaidInsurance).HasPrecision(18, 2);
        builder.Property(e => e.PaidAdmin).HasPrecision(18, 2);
        builder.Property(e => e.PaidOther).HasPrecision(18, 2);
        builder.Property(e => e.BalanceInsurance).HasPrecision(18, 2);
        builder.Property(e => e.BalanceAdmin).HasPrecision(18, 2);
        builder.Property(e => e.BalanceOther).HasPrecision(18, 2);
        builder.Property(e => e.DtfRate).HasPrecision(10, 5);
        builder.Property(e => e.SpreadPoints).HasPrecision(10, 5);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
