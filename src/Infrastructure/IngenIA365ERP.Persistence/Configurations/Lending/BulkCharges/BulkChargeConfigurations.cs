using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.BulkCharges;

// 2026-11-25 (pedido del dueño; feature 026): la corrida de cargos masivos y sus líneas (migración CargosMasivos, aditiva).

/// <summary><c>LND_BulkChargeRuns</c>: número único; línea, versión, persona de la contrapartida, corridas de fondo y usuarios con FK <c>Restrict</c>.</summary>
public class BulkChargeRunConfiguration : IEntityTypeConfiguration<BulkChargeRun>
{
    public const string Tabla = "LND_BulkChargeRuns";

    public void Configure(EntityTypeBuilder<BulkChargeRun> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Period).IsRequired();
        builder.Property(e => e.ChargeDate).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Installments).IsRequired();
        builder.Property(e => e.Periodicity).HasConversion<int>().IsRequired();
        builder.Property(e => e.ChannelMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.RepaymentChannel).HasConversion<int?>();
        builder.Property(e => e.PaymentCycle).HasConversion<int?>();
        builder.Property(e => e.FirstDueDate).IsRequired();
        builder.Property(e => e.CounterpartMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.AmortizationSystem).HasConversion<int?>();
        builder.Property(e => e.Insurances).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Source).HasConversion<int>().IsRequired();
        builder.Property(e => e.FiltersJson).IsRequired();
        builder.Property(e => e.PreviewSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.VoidReason).HasMaxLength(500);

        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLineVersion>().WithMany().HasForeignKey(e => e.CreditLineVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.CounterpartPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.VoidProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PreparedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_BulkChargeRuns_Number");
        builder.HasIndex(e => new { e.CreditLineId, e.Period }).HasDatabaseName("IX_LND_BulkChargeRuns_Line_Period");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_LND_BulkChargeRuns_Status");
        builder.HasIndex(e => e.CreditLineVersionId).HasDatabaseName("IX_LND_BulkChargeRuns_CreditLineVersionId");
        builder.HasIndex(e => e.CounterpartPersonId).HasDatabaseName("IX_LND_BulkChargeRuns_CounterpartPersonId");
        builder.HasIndex(e => e.ProcessRunId).HasDatabaseName("IX_LND_BulkChargeRuns_ProcessRunId");
        builder.HasIndex(e => e.VoidProcessRunId).HasDatabaseName("IX_LND_BulkChargeRuns_VoidProcessRunId");
        builder.HasIndex(e => e.PreparedByUserId).HasDatabaseName("IX_LND_BulkChargeRuns_PreparedByUserId");
        builder.HasIndex(e => e.ConfirmedByUserId).HasDatabaseName("IX_LND_BulkChargeRuns_ConfirmedByUserId");
        builder.HasIndex(e => e.VoidedByUserId).HasDatabaseName("IX_LND_BulkChargeRuns_VoidedByUserId");
    }
}

/// <summary>
/// <c>LND_BulkChargeLines</c>: una por asociado y corrida. El índice único filtrado por línea de crédito, persona y período sobre <c>HoldsPeriod</c>
/// impide un segundo cargo del mismo período mientras el anterior esté pendiente o creado.
/// </summary>
public class BulkChargeLineConfiguration : IEntityTypeConfiguration<BulkChargeLine>
{
    public const string Tabla = "LND_BulkChargeLines";

    public void Configure(EntityTypeBuilder<BulkChargeLine> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Period).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Installments).IsRequired();
        builder.Property(e => e.FirstDueDate).IsRequired();
        builder.Property(e => e.RepaymentChannel).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(60);
        builder.Property(e => e.ReasonDetail).HasMaxLength(500);
        builder.Property(e => e.Warning).HasMaxLength(60);
        builder.Property(e => e.HoldsPeriod).IsRequired();

        builder.HasOne<BulkChargeRun>().WithMany().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollAgreement>().WithMany().HasForeignKey(e => e.PayrollAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CreditLineId, e.PersonId, e.Period }).IsUnique()
            .HasDatabaseName("UK_LND_BulkChargeLines_Line_Person_Period").HasFilter("[HoldsPeriod] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.RunId, e.Status }).HasDatabaseName("IX_LND_BulkChargeLines_Run_Status");
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_LND_BulkChargeLines_PersonId");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_LND_BulkChargeLines_BranchId");
        builder.HasIndex(e => e.PayrollAgreementId).HasDatabaseName("IX_LND_BulkChargeLines_PayrollAgreementId");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_BulkChargeLines_LoanId");
    }
}
