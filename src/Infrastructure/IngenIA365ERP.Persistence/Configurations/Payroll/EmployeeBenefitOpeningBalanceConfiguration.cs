using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// Saldos iniciales de prestaciones (feature 010, data-model §2.3). Único
/// <c>(EmployeeId, AsOfDate, Kind)</c> entre vivas; el ajuste apunta a la fila que corrige y la
/// liquidación que lo consumió queda en <c>ConsumedByRunId</c>. Todo <c>Restrict</c>.
/// </summary>
public class EmployeeBenefitOpeningBalanceConfiguration : IEntityTypeConfiguration<EmployeeBenefitOpeningBalance>
{
    public void Configure(EntityTypeBuilder<EmployeeBenefitOpeningBalance> builder)
    {
        builder.ToTable("PAY_EmployeeBenefitOpeningBalances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.PendingVacationDays).HasPrecision(8, 2);
        builder.Property(e => e.AccruedSeverance).HasPrecision(18, 2);
        builder.Property(e => e.AccruedSeveranceInterest).HasPrecision(18, 2);
        builder.Property(e => e.AccruedServiceBonus).HasPrecision(18, 2);
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.AdjustmentReason).HasMaxLength(300);

        builder.HasIndex(e => new { e.EmployeeId, e.AsOfDate, e.Kind }).IsUnique()
            .HasDatabaseName("UK_PAY_EmployeeBenefitOpeningBalances_Employee_AsOf_Kind")
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AdjustsBalance).WithMany().HasForeignKey(e => e.AdjustsBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ConsumedByRun).WithMany().HasForeignKey(e => e.ConsumedByRunId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.EsEditable);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
