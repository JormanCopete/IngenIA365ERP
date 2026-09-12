using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// Transacciones de nómina (Principio XI): nunca se actualizan ni se borran. Cada
/// cálculo crea una corrida nueva; <c>(PayPeriodId, Version)</c> es la segunda barrera
/// contra dos cálculos simultáneos (la primera es el lock distribuido).
/// </summary>
public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.ToTable("PAY_PayrollRuns");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CalculatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ApprovedBy).HasMaxLength(100);
        builder.Property(e => e.ReversedBy).HasMaxLength(100);
        builder.Property(e => e.ReversalReason).HasMaxLength(300);
        builder.Property(e => e.DiscardedBy).HasMaxLength(100);
        builder.Property(e => e.DiscardReason).HasMaxLength(300);
        builder.Property(e => e.InputsHash).HasMaxLength(64).IsRequired();

        builder.Property(e => e.TotalEarnings).HasPrecision(18, 2);
        builder.Property(e => e.TotalDeductions).HasPrecision(18, 2);
        builder.Property(e => e.TotalEmployerContributions).HasPrecision(18, 2);
        builder.Property(e => e.TotalProvisions).HasPrecision(18, 2);
        builder.Property(e => e.TotalNet).HasPrecision(18, 2);
        builder.Property(e => e.RoundingAdjustment).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.PayPeriodId, e.Version }).IsUnique().HasDatabaseName("UK_PAY_PayrollRuns_Period_Version");
        builder.HasIndex(e => new { e.PayPeriodId, e.Status }).HasDatabaseName("IX_PAY_PayrollRuns_Period_Status");

        builder.HasOne(e => e.PayPeriod).WithMany().HasForeignKey(e => e.PayPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Employees).WithOne(x => x.Run).HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Restrict);

        // Comprobantes de aprobacion y de reversion: navegaciones para que la clave se
        // fije en el mismo SaveChanges (FR-023). Nunca se borra un comprobante con corrida.
        builder.HasOne(e => e.AccountingDocument).WithMany().HasForeignKey(e => e.AccountingDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReversalAccountingDocument).WithMany().HasForeignKey(e => e.ReversalAccountingDocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.IsEditableDraft);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PayrollRunEmployeeConfiguration : IEntityTypeConfiguration<PayrollRunEmployee>
{
    public void Configure(EntityTypeBuilder<PayrollRunEmployee> builder)
    {
        builder.ToTable("PAY_PayrollRunEmployees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.SalaryTranchesJson).IsRequired();
        builder.Property(e => e.BasesJson).IsRequired();
        builder.Property(e => e.NotesJson).IsRequired();
        builder.Property(e => e.TotalEarnings).HasPrecision(18, 2);
        builder.Property(e => e.TotalDeductions).HasPrecision(18, 2);
        builder.Property(e => e.TotalEmployerContributions).HasPrecision(18, 2);
        builder.Property(e => e.TotalProvisions).HasPrecision(18, 2);
        builder.Property(e => e.NetPay).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.PayrollRunId, e.EmployeeId }).IsUnique().HasDatabaseName("UK_PAY_PayrollRunEmployees_Run_Employee");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne(l => l.RunEmployee).HasForeignKey(l => l.PayrollRunEmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.HasBlockers);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PayrollRunLineConfiguration : IEntityTypeConfiguration<PayrollRunLine>
{
    public void Configure(EntityTypeBuilder<PayrollRunLine> builder)
    {
        builder.ToTable("PAY_PayrollRunLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.ConceptCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ConceptName).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ExplanationJson).IsRequired();

        builder.Property(e => e.Quantity).HasPrecision(10, 2);
        builder.Property(e => e.BaseAmount).HasPrecision(18, 2);
        builder.Property(e => e.Factor).HasPrecision(9, 4);
        builder.Property(e => e.RangeFrom).HasPrecision(18, 4);
        builder.Property(e => e.RangeTo).HasPrecision(18, 4);
        builder.Property(e => e.Amount).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.PayrollRunEmployeeId, e.Order }).HasDatabaseName("IX_PAY_PayrollRunLines_RunEmployee_Order");
        builder.HasIndex(e => e.ConceptCode).HasDatabaseName("IX_PAY_PayrollRunLines_ConceptCode");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
