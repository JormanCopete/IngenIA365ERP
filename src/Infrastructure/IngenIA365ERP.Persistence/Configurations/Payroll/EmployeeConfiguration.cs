using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    /// <summary>Largo del motivo de retiro; lo comparte el validador del comando (Principio VIII).</summary>
    public const int TerminationCauseMaxLength = 120;

    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("PAY_Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_Employees_PublicId");

        // PersonId is now NOT NULL (every Employee belongs to a Person)
        builder.Property(e => e.PersonId).IsRequired();
        // Feature 008 (FR-017): una persona tiene a lo sumo UNA ficha viva, pero puede tener
        // varias a lo largo del tiempo — cada reingreso tras un retiro es una ficha nueva y la
        // retirada queda como historial de sus liquidaciones. Hasta el 2026-09-13 el índice era
        // único sin filtro: el reingreso pasaba la validación (sólo mira fichas vivas) y
        // reventaba en la base con un 500. El filtro va en la sintaxis T-SQL canónica;
        // ProviderModelConventions lo traduce para PostgreSQL.
        builder.HasIndex(e => e.PersonId).IsUnique()
            .HasDatabaseName("UK_PAY_Employees_PersonId")
            .HasFilter("[Status] <> -1 AND [IsDeleted] = 0");

        // === Labor data ===
        builder.Property(e => e.CostCenterId).HasMaxLength(8).IsRequired();
        builder.Property(e => e.AreaCode).HasMaxLength(4);
        builder.Property(e => e.SectionId).HasMaxLength(4);
        builder.Property(e => e.Salary).HasPrecision(18, 2);
        builder.Property(e => e.WithholdingTaxRate).HasPrecision(7, 4);
        builder.Property(e => e.DeductibleWithholding).HasPrecision(17, 4);
        // Motivo de retiro. En SOLIDO era un código de 4 caracteres; aquí la pantalla lo pide
        // como texto libre y nada lo interpreta como código, así que desde el 2026-09-13 admite
        // 120 (migración MotivoDeRetiroComoTexto). Antes cualquier motivo de más de 4 daba 500.
        builder.Property(e => e.TerminationCause).HasMaxLength(TerminationCauseMaxLength);

        // === Payroll banking ===
        builder.Property(e => e.PayrollBankId).HasMaxLength(4);
        builder.Property(e => e.PayrollBankAccountNumber).HasMaxLength(25);

        // === Bonuses & provisions ===
        builder.Property(e => e.RepresentationExpense).HasPrecision(18, 2);
        builder.Property(e => e.TechnicalBonus).HasPrecision(18, 2);
        builder.Property(e => e.OtherBonus).HasPrecision(18, 2);
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

        // Feature 005: cada empleado pertenece a exactamente un plan de nomina (FR-037).
        builder.HasOne(e => e.PayrollPlan)
            .WithMany()
            .HasForeignKey(e => e.PayrollPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.PayrollPlanId).HasDatabaseName("IX_PAY_Employees_PayrollPlan");

        // Procedimiento de retencion 1 o 2 (FR-015). El default va en la BASE, no solo
        // en el CLR: la migracion que anade la columna deja a los empleados existentes
        // en el procedimiento 1, no en un 0 que ningun calculo acepta.
        builder.Property(e => e.WithholdingProcedure).HasDefaultValue((byte)1);

        // === Audit ===
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
