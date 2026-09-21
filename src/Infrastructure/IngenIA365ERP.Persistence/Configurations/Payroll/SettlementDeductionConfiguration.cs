using IngenIA365ERP.Domain.Entities.Lending;
using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// Descuentos de la definitiva (feature 010, data-model §2.6). Un préstamo o una libranza se
/// propone una sola vez por terminación: dos únicos filtrados sobre la columna no nula, porque
/// un único sin filtro con la columna nullable se comporta distinto en cada motor (SQL Server:
/// un solo NULL; PostgreSQL: NULLs distintos). Las FKs a Cartera y a la libranza van sin
/// navegación (ver la entidad).
/// </summary>
public class SettlementDeductionConfiguration : IEntityTypeConfiguration<SettlementDeduction>
{
    public void Configure(EntityTypeBuilder<SettlementDeduction> builder)
    {
        builder.ToTable("PAY_SettlementDeductions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ProposedAmount).HasPrecision(18, 2);
        builder.Property(e => e.AppliedAmount).HasPrecision(18, 2);
        builder.Property(e => e.RemainingBalanceAfter).HasPrecision(18, 2);
        builder.Property(e => e.AdjustmentReason).HasMaxLength(300);
        builder.Property(e => e.AdjustedBy).HasMaxLength(100);

        // Únicos sólo entre filas vivas: el recálculo retira en blando la deuda que Cartera ya no trae y, si la misma
        // obligación vuelve (un pago reversado en Cartera), crea otra fila con la misma llave. Hasta la revisión N1
        // (2026-09-21) el filtro no excluía las eliminadas y ese INSERT respondía 500 (migración SettlementDeductionsUnicosEntreVivas).
        builder.HasIndex(e => new { e.TerminationId, e.LoanPortfolioId }).IsUnique()
            .HasDatabaseName("UK_PAY_SettlementDeductions_Termination_Loan")
            .HasFilter("[LoanPortfolioId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.TerminationId, e.RecurringNoveltyId }).IsUnique()
            .HasDatabaseName("UK_PAY_SettlementDeductions_Termination_Libranza")
            .HasFilter("[RecurringNoveltyId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne<LoanPortfolio>().WithMany().HasForeignKey(e => e.LoanPortfolioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollRecurringNovelty>().WithMany().HasForeignKey(e => e.RecurringNoveltyId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.FueAjustado);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
