using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PreLiquidationConfiguration : IEntityTypeConfiguration<PreLiquidation>
{
    public void Configure(EntityTypeBuilder<PreLiquidation> builder)
    {
        builder.ToTable("PAY_PreLiquidations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Value).HasPrecision(18, 2);
        builder.Property(e => e.BasicSalary).HasPrecision(18, 2);
        builder.Property(e => e.Ibc).HasPrecision(18, 2);
        builder.Property(e => e.HealthValue).HasPrecision(18, 2);
        builder.Property(e => e.PensionValue).HasPrecision(18, 2);
        builder.Property(e => e.WorkRiskValue).HasPrecision(18, 2);
        builder.Property(e => e.SolidarityValue).HasPrecision(18, 2);
        builder.Property(e => e.MaternityValue).HasPrecision(18, 2);
        builder.Property(e => e.GeneralValue).HasPrecision(18, 2);
        builder.Property(e => e.WorkRiskRate).HasPrecision(5, 3);
        builder.Property(e => e.CurrentSalary).HasPrecision(18, 2);
        builder.Property(e => e.MinimumWage).HasPrecision(18, 2);
        builder.Property(e => e.IbcWorkRisk).HasPrecision(18, 2);
        builder.Property(e => e.CurrentSalaryFull).HasPrecision(18, 2);
        builder.Property(e => e.ClassCode).HasMaxLength(1).IsRequired();
        builder.Property(e => e.PensionEntry).HasMaxLength(1).IsRequired();
        builder.Property(e => e.HealthEntry).HasMaxLength(1).IsRequired();
        builder.Property(e => e.WorkRiskEntry).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsNewHire).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsTermination).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsRateChange).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsEntityChange).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsSuspensionPension).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsSuspensionTemp).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsUnpaidLeave).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsGeneralIncapacity).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsMaternityLeave).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsVacation).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsTemporaryTransfer).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsVoluntaryPension).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsWorkRiskIncapacity).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsVacationCause).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsVacationEnjoyment).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsExtension).HasMaxLength(1).IsRequired();
        builder.Property(e => e.SenaContrib).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsIntegralSalary).HasMaxLength(1).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(15).IsRequired();
        builder.Property(e => e.EmployeeName).HasMaxLength(40).IsRequired();
        builder.Property(e => e.PilaPensionCode).HasMaxLength(6).IsRequired();
        builder.Property(e => e.PilaHealthCode).HasMaxLength(6).IsRequired();
        builder.Property(e => e.PilaWorkRiskCode).HasMaxLength(6).IsRequired();
        builder.Property(e => e.PilaCcfCode).HasMaxLength(6).IsRequired();

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
