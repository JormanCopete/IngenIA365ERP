using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Porcentaje fijo del procedimiento 2, con vigencia semestral (FR-039).</summary>
public class EmployeeWithholdingRateConfiguration : IEntityTypeConfiguration<EmployeeWithholdingRate>
{
    public void Configure(EntityTypeBuilder<EmployeeWithholdingRate> builder)
    {
        builder.ToTable("PAY_EmployeeWithholdingRates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.RatePercent).HasPrecision(6, 3);

        builder.HasIndex(e => new { e.EmployeeId, e.ValidFrom }).HasDatabaseName("IX_PAY_EmployeeWithholdingRates_Employee_ValidFrom");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Deducciones y rentas exentas declaradas para depurar la base de retención.</summary>
public class EmployeeTaxDeductionConfiguration : IEntityTypeConfiguration<EmployeeTaxDeduction>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxDeduction> builder)
    {
        builder.ToTable("PAY_EmployeeTaxDeductions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.MonthlyAmount).HasPrecision(18, 2);
        builder.Property(e => e.Percent).HasPrecision(9, 4);

        builder.HasIndex(e => new { e.EmployeeId, e.Kind, e.ValidFrom }).HasDatabaseName("IX_PAY_EmployeeTaxDeductions_Employee_Kind_ValidFrom");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
