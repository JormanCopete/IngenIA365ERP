using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class ChartOfAccountConfiguration : IEntityTypeConfiguration<ChartOfAccount>
{
    public void Configure(EntityTypeBuilder<ChartOfAccount> builder)
    {
        builder.ToTable("ACC_ChartOfAccounts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.LegacyCode).HasMaxLength(20);
        builder.HasIndex(e => e.LegacyCode);

        builder.Property(e => e.AccountCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => e.AccountCode).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Nature).HasMaxLength(1).IsRequired();
        builder.Property(e => e.Rate).HasPrecision(10, 4);
        builder.Property(e => e.CostCenterCode).HasMaxLength(20);
        builder.Property(e => e.FixedAssetGroup).HasMaxLength(10);
        builder.Property(e => e.FixedAssetClass).HasMaxLength(10);
        builder.Property(e => e.CashFlowCode).HasMaxLength(10);
        builder.Property(e => e.BankReconciliationCode).HasMaxLength(10);
        builder.Property(e => e.WithholdingType).HasMaxLength(5);
        builder.Property(e => e.AccountBelongsTo).HasMaxLength(5);
        builder.Property(e => e.AccountGroup).HasMaxLength(10);
        builder.Property(e => e.WithholdingLineCode).HasMaxLength(10);
        builder.Property(e => e.IcaLineCode).HasMaxLength(10);
        builder.Property(e => e.VatLineCode).HasMaxLength(10);
        builder.Property(e => e.SalesWithholdingLineCode).HasMaxLength(10);
        builder.Property(e => e.GmfLineCode).HasMaxLength(10);
        builder.Property(e => e.IcaBaseLineCode).HasMaxLength(10);
        builder.Property(e => e.GmfBaseLineCode).HasMaxLength(10);

        builder.HasMany(e => e.Balances).WithOne(b => b.Account).HasForeignKey(b => b.AccountId);
        builder.HasMany(e => e.JournalEntries).WithOne(j => j.Account).HasForeignKey(j => j.AccountId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
