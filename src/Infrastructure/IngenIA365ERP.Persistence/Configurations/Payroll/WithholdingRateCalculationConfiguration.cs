using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// Cálculo del porcentaje del procedimiento 2 (feature 010, data-model §2.7): versionado por
/// <c>(EmployeeId, TargetYear, TargetSemester, Version)</c>. La vigencia que abre se referencia
/// sin navegación (la vigencia apunta de vuelta con <c>SourceCalculationId</c>).
/// </summary>
public class WithholdingRateCalculationConfiguration : IEntityTypeConfiguration<WithholdingRateCalculation>
{
    public void Configure(EntityTypeBuilder<WithholdingRateCalculation> builder)
    {
        builder.ToTable("PAY_WithholdingRateCalculations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CalculatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ApprovedBy).HasMaxLength(100);
        builder.Property(e => e.RejectReason).HasMaxLength(300);
        builder.Property(e => e.DepurationSequence).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ExplanationJson).IsRequired();

        builder.Property(e => e.Divisor).HasPrecision(6, 2);
        builder.Property(e => e.TotalGrossIncome).HasPrecision(18, 2);
        builder.Property(e => e.TotalMandatoryContributions).HasPrecision(18, 2);
        builder.Property(e => e.TotalDeclaredDeductions).HasPrecision(18, 2);
        builder.Property(e => e.TotalExemptIncome).HasPrecision(18, 2);
        builder.Property(e => e.DepuratedBase).HasPrecision(18, 2);
        builder.Property(e => e.AverageMonthlyBase).HasPrecision(18, 2);
        builder.Property(e => e.UvtValueUsed).HasPrecision(18, 4);
        builder.Property(e => e.AverageInUvt).HasPrecision(18, 4);
        builder.Property(e => e.TheoreticalWithholding).HasPrecision(18, 2);
        builder.Property(e => e.RatePercent).HasPrecision(6, 3);

        builder.HasIndex(e => new { e.EmployeeId, e.TargetYear, e.TargetSemester, e.Version }).IsUnique()
            .HasDatabaseName("UK_PAY_WithholdingRateCalculations_Employee_Target_Version");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_PAY_WithholdingRateCalculations_Status");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TableParameter).WithMany().HasForeignKey(e => e.TableParameterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeWithholdingRate>().WithMany().HasForeignKey(e => e.ResultingRateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Months).WithOne(m => m.Calculation).HasForeignKey(m => m.CalculationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class WithholdingRateCalculationMonthConfiguration : IEntityTypeConfiguration<WithholdingRateCalculationMonth>
{
    public void Configure(EntityTypeBuilder<WithholdingRateCalculationMonth> builder)
    {
        builder.ToTable("PAY_WithholdingRateCalculationMonths");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.GrossIncome).HasPrecision(18, 2);
        builder.Property(e => e.MandatoryContributions).HasPrecision(18, 2);
        builder.Property(e => e.SourceRunsJson).IsRequired();

        builder.HasIndex(e => new { e.CalculationId, e.Year, e.Month }).IsUnique()
            .HasDatabaseName("UK_PAY_WithholdingRateCalculationMonths_Calculation_Year_Month");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
