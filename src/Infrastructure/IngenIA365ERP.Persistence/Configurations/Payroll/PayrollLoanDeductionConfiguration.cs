using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// <c>PAY_PayrollLoanDeductions</c> (feature 013, T0155; data-model parte 3 §7.1): un descuento de Cartera por corrida, empleado y
/// obligación, único entre las filas vivas. Sin FK a <c>LND_</c> (FR-008, <c>LaCarteraSoloSeTocaPorSusInterfaces</c>): la obligación
/// es un <c>PublicId</c>. (nuevo)
/// </summary>
public class PayrollLoanDeductionConfiguration : IEntityTypeConfiguration<PayrollLoanDeduction>
{
    public void Configure(EntityTypeBuilder<PayrollLoanDeduction> builder)
    {
        builder.ToTable("PAY_PayrollLoanDeductions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.ObligationNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ExpectedAmount).HasPrecision(18, 2);
        builder.Property(e => e.DeductedAmount).HasPrecision(18, 2);
        builder.Property(e => e.NotDeductedAmount).HasPrecision(18, 2);
        builder.Property(e => e.AppliedAmount).HasPrecision(18, 2);
        builder.Property(e => e.NotDeductedReason).HasMaxLength(20);
        builder.Property(e => e.ResultCode).HasMaxLength(80);
        builder.Property(e => e.ResultMessage).HasMaxLength(500);
        builder.Property(e => e.Status).IsRequired();

        builder.HasIndex(e => new { e.PayrollRunId, e.PayrollRunEmployeeId, e.ObligationPublicId }).IsUnique()
            .HasDatabaseName("UK_PAY_PayrollLoanDeductions_Run_Employee_Obligation")
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.PayrollRun).WithMany().HasForeignKey(e => e.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PayrollRunEmployee).WithMany().HasForeignKey(e => e.PayrollRunEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PayrollRunLine).WithMany().HasForeignKey(e => e.PayrollRunLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
