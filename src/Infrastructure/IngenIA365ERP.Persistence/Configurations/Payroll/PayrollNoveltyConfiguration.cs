using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PayrollNoveltyConfiguration : IEntityTypeConfiguration<PayrollNovelty>
{
    public void Configure(EntityTypeBuilder<PayrollNovelty> builder)
    {
        builder.ToTable("PAY_Novelties");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.ConceptCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.StatusReason).HasMaxLength(300);
        builder.Property(e => e.Quantity).HasPrecision(10, 2);
        builder.Property(e => e.Amount).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.PayPeriodId, e.EmployeeId, e.Status }).HasDatabaseName("IX_PAY_Novelties_Period_Employee_Status");
        builder.HasIndex(e => new { e.PayPeriodId, e.ConceptDefinitionId }).HasDatabaseName("IX_PAY_Novelties_Period_Concept");
        builder.HasIndex(e => e.ImportBatchId).HasDatabaseName("IX_PAY_Novelties_ImportBatch");

        builder.HasOne(e => e.PayPeriod).WithMany().HasForeignKey(e => e.PayPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ConceptDefinition).WithMany().HasForeignKey(e => e.ConceptDefinitionId).OnDelete(DeleteBehavior.Restrict);
        // Feature 010 (R6): la novedad que dejó un disfrute de vacaciones apunta a su movimiento.
        builder.HasOne<VacationMovement>().WithMany().HasForeignKey(e => e.VacationMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.VacationMovementId).HasDatabaseName("IX_PAY_Novelties_VacationMovement");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PayrollRecurringNoveltyConfiguration : IEntityTypeConfiguration<PayrollRecurringNovelty>
{
    public void Configure(EntityTypeBuilder<PayrollRecurringNovelty> builder)
    {
        builder.ToTable("PAY_RecurringNovelties");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.ConceptCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.DeactivationReason).HasMaxLength(300);
        builder.Property(e => e.Quantity).HasPrecision(10, 2);
        builder.Property(e => e.Amount).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.EmployeeId, e.IsActive }).HasDatabaseName("IX_PAY_RecurringNovelties_Employee_Active");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.HasInstallmentsLeft);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
