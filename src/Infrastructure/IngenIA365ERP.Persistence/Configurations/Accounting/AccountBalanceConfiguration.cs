using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class AccountBalanceConfiguration : IEntityTypeConfiguration<AccountBalance>
{
    public void Configure(EntityTypeBuilder<AccountBalance> builder)
    {
        builder.ToTable("ACC_AccountBalances");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.AccountId, e.PeriodYear, e.PeriodMonth, e.BranchId, e.CostCenterId }).IsUnique();

        builder.HasOne(e => e.Account).WithMany(a => a.Balances).HasForeignKey(e => e.AccountId);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
