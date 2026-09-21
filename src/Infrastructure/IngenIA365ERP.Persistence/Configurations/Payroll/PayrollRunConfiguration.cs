using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// Transacciones de nómina (Principio XI): nunca se actualizan ni se borran. Cada
/// cálculo crea una corrida nueva; el índice único por tipo es la segunda barrera
/// contra dos cálculos simultáneos (la primera es el lock distribuido).
///
/// <para>
/// Feature 010 (R2): la corrida tiene <c>Kind</c> y el período es opcional. El único
/// <c>(PayPeriodId, Version)</c> de la 005 se reemplaza por <b>cinco filtrados</b>, uno por
/// tipo (dos índices en vez de un <c>IN</c> para no depender de que el traductor de filtros
/// entienda <c>OR</c>). El default de <c>Kind</c> va <b>en la base</b>, no sólo en el CLR
/// (lección de <c>DosContextosUnaTablaTests</c>): las corridas de producción quedan
/// <c>Ordinary</c> sin migración de datos y cualquier INSERT que omita la columna también.
/// </para>
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

        builder.Property(e => e.Kind).HasDefaultValue(PayrollRunKind.Ordinary);
        builder.Property(e => e.PayPeriodId).IsRequired(false);

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

        // Unicidad por tipo (data-model 010 §1.1). Los filtros van en T-SQL canónico y
        // ProviderModelConventions los traduce para PostgreSQL. El de la ordinaria es el mismo
        // (PayPeriodId, Version) de antes, ahora con filtro: sin él, dos corridas especiales con
        // PayPeriodId NULL colisionarían en SQL Server (un solo NULL por índice único).
        builder.HasIndex(e => new { e.PayPeriodId, e.Version }).IsUnique()
            .HasDatabaseName("UK_PAY_PayrollRuns_Ordinary_Period_Version")
            .HasFilter("[Kind] = 0");
        builder.HasIndex(e => new { e.Year, e.Semester, e.Version }).IsUnique()
            .HasDatabaseName("UK_PAY_PayrollRuns_ServiceBonus_Year_Semester_Version")
            .HasFilter("[Kind] = 1");
        builder.HasIndex(e => new { e.Year, e.Version }).IsUnique()
            .HasDatabaseName("UK_PAY_PayrollRuns_Severance_Year_Version")
            .HasFilter("[Kind] = 2");
        // Vacaciones y definitiva comparten columnas: el nombre va en HasIndex, porque con las
        // mismas propiedades EF devuelve el MISMO índice y el segundo pisaría al primero.
        builder.HasIndex(e => new { e.EmployeeId, e.CutoffDate, e.Version }, "UK_PAY_PayrollRuns_Vacation_Employee_Cutoff_Version").IsUnique()
            .HasFilter("[Kind] = 3");
        builder.HasIndex(e => new { e.EmployeeId, e.CutoffDate, e.Version }, "UK_PAY_PayrollRuns_Settlement_Employee_Cutoff_Version").IsUnique()
            .HasFilter("[Kind] = 4");

        builder.HasIndex(e => new { e.PayPeriodId, e.Status }).HasDatabaseName("IX_PAY_PayrollRuns_Period_Status");
        builder.HasIndex(e => new { e.Kind, e.Status }).HasDatabaseName("IX_PAY_PayrollRuns_Kind_Status");

        builder.HasOne(e => e.PayPeriod).WithMany().HasForeignKey(e => e.PayPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Employees).WithOne(x => x.Run).HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Restrict);

        // Feature 010: las corridas de un solo empleado (vacaciones, definitiva) y sus orígenes.
        // Restrict siempre: nada se borra en cascada en nómina.
        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Termination).WithMany().HasForeignKey(e => e.TerminationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.VacationMovement).WithMany().HasForeignKey(e => e.VacationMovementId).OnDelete(DeleteBehavior.Restrict);

        // Comprobantes de aprobacion y de reversion: navegaciones para que la clave se
        // fije en el mismo SaveChanges (FR-023). Nunca se borra un comprobante con corrida.
        builder.HasOne(e => e.AccountingDocument).WithMany().HasForeignKey(e => e.AccountingDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReversalAccountingDocument).WithMany().HasForeignKey(e => e.ReversalAccountingDocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.IsEditableDraft);
        builder.Ignore(e => e.EsEspecial);
        builder.Ignore(e => e.EsCoherente);
        builder.Ignore(e => e.SourceTypeName);

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

        // Feature 010 (FR-018a): la línea DESC_CARTERA / LIBRANZA de una definitiva apunta al descuento del que salió.
        builder.HasOne<SettlementDeduction>().WithMany().HasForeignKey(e => e.SettlementDeductionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
