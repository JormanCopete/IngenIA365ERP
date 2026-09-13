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
        // Indice de lectura (2026-09-13): saldos, balance de prueba y estados financieros
        // piden un (anio, mes) para miles de cuentas a la vez (AccountBalanceQueries); con
        // el periodo adelante el motor localiza el mes y lee solo esas filas, en vez de
        // buscar cuenta por cuenta en el indice unico que empieza por AccountId.
        builder.HasIndex(e => new { e.PeriodYear, e.PeriodMonth, e.AccountId }).HasDatabaseName("IX_ACC_AccountBalances_Period_Account");

        builder.HasOne(e => e.Account).WithMany(a => a.Balances).HasForeignKey(e => e.AccountId);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
