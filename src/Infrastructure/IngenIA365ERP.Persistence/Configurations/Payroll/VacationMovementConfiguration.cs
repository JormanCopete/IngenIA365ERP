using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// Movimientos de vacaciones (feature 010, data-model §2.4). Sin índice único: que dos disfrutes
/// activos del mismo empleado no se crucen en fechas es regla del comando. La FK a la corrida que
/// lo liquidó se declara aquí sin navegación (la corrida ya navega al movimiento que la originó).
/// </summary>
public class VacationMovementConfiguration : IEntityTypeConfiguration<VacationMovement>
{
    public void Configure(EntityTypeBuilder<VacationMovement> builder)
    {
        builder.ToTable("PAY_VacationMovements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.BusinessDays).HasPrecision(8, 2);
        builder.Property(e => e.WeekPolicyUsed).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SkippedDaysJson).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(300);
        builder.Property(e => e.CancelReason).HasMaxLength(300);

        builder.HasIndex(e => new { e.EmployeeId, e.StartDate }).HasDatabaseName("IX_PAY_VacationMovements_Employee_Start");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_PAY_VacationMovements_Status");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollRun>().WithMany().HasForeignKey(e => e.PayrollRunId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.EstaVivo);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
