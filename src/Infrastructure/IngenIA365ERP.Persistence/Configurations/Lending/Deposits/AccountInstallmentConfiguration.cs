using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

/// <summary>
/// <c>LND_AccountInstallments</c> (feature 014, US3, T081; data-model §3.5): hecho <c>bigint</c>, una cuota por cuenta y período (la idempotencia del
/// paso de captaciones), valor mayor que cero, exactamente un origen (la corrida o la operación) y la hermana del ahorro permanente por autorreferencia.
/// Migración <c>CaptacionesAportes</c>.
/// </summary>
public class AccountInstallmentConfiguration : IEntityTypeConfiguration<AccountInstallment>
{
    public const string Tabla = "LND_AccountInstallments";

    public void Configure(EntityTypeBuilder<AccountInstallment> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_AccountInstallments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_LND_AccountInstallments_Origin",
                "([GeneratedByProcessRunId] IS NOT NULL AND [GeneratedByOperationId] IS NULL) OR ([GeneratedByProcessRunId] IS NULL AND [GeneratedByOperationId] IS NOT NULL)");
        });

        builder.Property(e => e.PeriodYear).IsRequired();
        builder.Property(e => e.PeriodMonth).IsRequired();
        builder.Property(e => e.SubPeriodNumber).IsRequired();
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.ContributionShare).Monto();
        builder.Property(e => e.PermanentSavingsShare).Monto();
        builder.Property(e => e.RulesVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.CancelReason).HasMaxLength(AccountInstallment.LargoDelMotivo);
        builder.Ignore(e => e.Periodo);
        builder.Ignore(e => e.Anulada);

        builder.HasOne(e => e.MemberAccount).WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.LinkedInstallment).WithMany().HasForeignKey(e => e.LinkedInstallmentId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AccountInstallments_Linked");
        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.GeneratedByProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.GeneratedByOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AccountInstallments_GeneratedByOperation");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.CancelledByOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AccountInstallments_CancelledByOperation");

        builder.HasIndex(e => new { e.MemberAccountId, e.PeriodYear, e.PeriodMonth, e.SubPeriodNumber }).IsUnique()
            .HasDatabaseName("UK_LND_AccountInstallments_Account_Period");
        builder.HasIndex(e => new { e.MemberAccountId, e.DueDate }).HasDatabaseName("IX_LND_AccountInstallments_Account_DueDate");
        builder.HasIndex(e => e.DueDate).HasDatabaseName("IX_LND_AccountInstallments_DueDate").HasFilter("[CancelledAt] IS NULL");
        builder.HasIndex(e => e.LinkedInstallmentId).HasDatabaseName("IX_LND_AccountInstallments_Linked").HasFilter("[LinkedInstallmentId] IS NOT NULL");
        builder.HasIndex(e => e.GeneratedByProcessRunId).HasDatabaseName("IX_LND_AccountInstallments_ProcessRun")
            .HasFilter("[GeneratedByProcessRunId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_AccountInstallmentNonDeductions</c> (feature 014, US3, T081; data-model §3.7): hecho; único por cuota, recaudador y clave (un reintento no lo
/// duplica). Migración <c>CaptacionesAportes</c>.
/// </summary>
public class AccountInstallmentNonDeductionConfiguration : IEntityTypeConfiguration<AccountInstallmentNonDeduction>
{
    public const string Tabla = "LND_AccountInstallmentNonDeductions";

    public void Configure(EntityTypeBuilder<AccountInstallmentNonDeduction> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedAmount).Monto().IsRequired();
        builder.Property(e => e.DeductedAmount).Monto().IsRequired();
        builder.Property(e => e.CollectorCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ReferenceKey).HasMaxLength(150).IsRequired();
        builder.Property(e => e.OccurredOn).IsRequired();
        builder.Property(e => e.Detail).HasMaxLength(300);

        builder.HasOne<AccountInstallment>().WithMany().HasForeignKey(e => e.InstallmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.InstallmentId, e.CollectorCode, e.ReferenceKey }).IsUnique()
            .HasDatabaseName("UK_LND_AccountInstallmentNonDeductions_Inst_Collector_Key");
    }
}
