using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PayPeriodConfiguration : IEntityTypeConfiguration<PayPeriod>
{
    public void Configure(EntityTypeBuilder<PayPeriod> builder)
    {
        builder.ToTable("PAY_PayPeriods");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.PlanId, e.PayrollCompanyId }).IsUnique();

        builder.Property(e => e.Description).HasMaxLength(100);
        builder.Property(e => e.PayDate).HasMaxLength(40);
        builder.Property(e => e.LiquidationCompanyId).HasMaxLength(4);
        builder.Property(e => e.OnlyEntries).HasMaxLength(1);
        builder.Property(e => e.NoAutoSalaryLiq).HasMaxLength(1);
        builder.Property(e => e.NoAbsenceLiq).HasMaxLength(1);
        builder.Property(e => e.NoDirectDebitLiq).HasMaxLength(1);
        builder.Property(e => e.StatusMessage).HasMaxLength(100).IsRequired();
        builder.Property(e => e.AdvanceLiquidation).HasMaxLength(1).IsRequired();
        builder.Property(e => e.AdvanceCrossing).HasMaxLength(1).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
