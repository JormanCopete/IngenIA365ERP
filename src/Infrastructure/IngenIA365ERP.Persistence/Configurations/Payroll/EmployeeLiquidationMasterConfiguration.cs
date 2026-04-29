using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class EmployeeLiquidationMasterConfiguration : IEntityTypeConfiguration<EmployeeLiquidationMaster>
{
    public void Configure(EntityTypeBuilder<EmployeeLiquidationMaster> builder)
    {
        builder.ToTable("PAY_EmployeeLiquidationMasters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Salary).HasPrecision(18, 2);
        builder.Property(e => e.PreviousSeveranceAmount).HasPrecision(18, 2);
        builder.Property(e => e.CurrentSeveranceAmount).HasPrecision(18, 2);
        builder.Property(e => e.SeveranceBase).HasPrecision(18, 2);
        builder.Property(e => e.BonusBase).HasPrecision(18, 2);
        builder.Property(e => e.VacationBase).HasPrecision(18, 2);
        builder.Property(e => e.IndemnityBase).HasPrecision(18, 2);
        builder.Property(e => e.SeveranceDays).HasPrecision(18, 2);
        builder.Property(e => e.BonusDays).HasPrecision(18, 2);
        builder.Property(e => e.VacationDays).HasPrecision(18, 2);
        builder.Property(e => e.IndemnityDays).HasPrecision(18, 2);
        builder.Property(e => e.SpecialRegime).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IsAccountingPosted).HasMaxLength(1).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(14).IsRequired();

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
