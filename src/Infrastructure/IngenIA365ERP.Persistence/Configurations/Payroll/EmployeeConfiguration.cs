using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("PAY_Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_Employees_PublicId");

        // PersonId is now NOT NULL (every Employee belongs to a Person)
        builder.Property(e => e.PersonId).IsRequired();
        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName("UK_PAY_Employees_PersonId");

        // === Labor data ===
        builder.Property(e => e.CostCenterId).HasMaxLength(8).IsRequired();
        builder.Property(e => e.AreaCode).HasMaxLength(4);
        builder.Property(e => e.SectionId).HasMaxLength(4);
        builder.Property(e => e.Salary).HasPrecision(18, 2);
        builder.Property(e => e.WithholdingTaxRate).HasPrecision(7, 4);
        builder.Property(e => e.DeductibleWithholding).HasPrecision(17, 4);
        builder.Property(e => e.TerminationCause).HasMaxLength(4);

        // === Payroll banking ===
        builder.Property(e => e.PayrollBankId).HasMaxLength(4);
        builder.Property(e => e.PayrollBankAccountNumber).HasMaxLength(25);

        // === Bonuses & provisions ===
        builder.Property(e => e.RepresentationExpense).HasPrecision(18, 2);
        builder.Property(e => e.TechnicalBonus).HasPrecision(18, 2);
        builder.Property(e => e.OtherBonus).HasPrecision(18, 2);
        builder.Property(e => e.SeveranceFundId).HasPrecision(6, 0);
        builder.Property(e => e.BonusDays).HasPrecision(10, 0);
        builder.Property(e => e.VacationDays).HasPrecision(10, 0);
        builder.Property(e => e.IndemnityDays).HasPrecision(10, 0);
        builder.Property(e => e.SeveranceAvgDays).HasPrecision(10, 0);
        builder.Property(e => e.BonusAvgDays).HasPrecision(10, 0);
        builder.Property(e => e.IndemnityAvgDays).HasPrecision(10, 0);
        builder.Property(e => e.VacationAvgDays).HasPrecision(10, 0);
        builder.Property(e => e.SeveranceDaysCalc).HasPrecision(10, 0);
        builder.Property(e => e.IndemnityDaysCalc).HasPrecision(10, 0);

        builder.Property(e => e.PensionFundMember).HasMaxLength(1);
        builder.Property(e => e.IsLiquidated).HasMaxLength(1);
        builder.Property(e => e.SpecialRegime).HasMaxLength(1);
        builder.Property(e => e.ExtraBonusFlag).HasMaxLength(1);

        // === Relationships ===
        builder.HasOne(e => e.Person)
            .WithMany()
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.PayrollCompanyId);
        builder.HasIndex(e => e.Status);

        // === Audit ===
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
