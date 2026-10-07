using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

/// <summary>
/// <c>LND_InstallmentNonDeductions</c> (feature 013; data-model §24.9): hecho; idempotente por clave del contrato y cuota, y por
/// activación y cuota (únicos filtrados a no nulo). La cuota es <c>bigint</c>. El único de <c>PayrollDeductionResultId</c> nace con K2.
/// </summary>
public class InstallmentNonDeductionConfiguration : IEntityTypeConfiguration<InstallmentNonDeduction>
{
    public const string Tabla = "LND_InstallmentNonDeductions";

    public void Configure(EntityTypeBuilder<InstallmentNonDeduction> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.InstallmentNumber).IsRequired();
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasConversion<int>().IsRequired();
        builder.Property(e => e.ReasonText).HasMaxLength(300);
        builder.Property(e => e.ExpectedAmount).Monto().IsRequired();
        builder.Property(e => e.NotDeductedAmount).Monto().IsRequired();
        builder.Property(e => e.CollectorCode).HasMaxLength(20);
        builder.Property(e => e.ContractOperationKey).HasMaxLength(120);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RepaymentInstallment>().WithMany().HasForeignKey(e => e.RepaymentInstallmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingActivation>().WithMany().HasForeignKey(e => e.ActivationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ContractOperationKey, e.RepaymentInstallmentId }).IsUnique()
            .HasDatabaseName("UK_LND_InstallmentNonDeductions_ContractKey_Inst").HasFilter("[ContractOperationKey] IS NOT NULL");
        builder.HasIndex(e => new { e.ActivationId, e.RepaymentInstallmentId }).IsUnique()
            .HasDatabaseName("UK_LND_InstallmentNonDeductions_Activation_Inst").HasFilter("[ActivationId] IS NOT NULL");
        builder.HasIndex(e => new { e.LoanId, e.DueDate }).HasDatabaseName("IX_LND_InstallmentNonDeductions_Loan_DueDate");
    }
}
