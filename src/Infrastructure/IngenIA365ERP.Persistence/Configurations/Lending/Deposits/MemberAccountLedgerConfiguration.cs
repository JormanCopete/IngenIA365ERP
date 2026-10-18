using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

/// <summary>
/// <c>LND_MemberAccountOperations</c> (feature 014, K7; data-model §4.1): hecho <c>bigint</c>, comprobante único, secuencia única por cuenta,
/// idempotencia por recaudador y clave (SC-005), un reverso por operación e índice de lo que sigue en canje. Las clases de recaudador, de medio y la
/// validación previa viajan como texto.
/// </summary>
public class MemberAccountOperationConfiguration : IEntityTypeConfiguration<MemberAccountOperation>
{
    public const string Tabla = "LND_MemberAccountOperations";

    public void Configure(EntityTypeBuilder<MemberAccountOperation> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Sequence).IsRequired();
        builder.Property(e => e.ValueDate).IsRequired();
        builder.Property(e => e.AccountingDate).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();

        builder.Property(e => e.ActorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.CollectorClass).HasMaxLength(30);
        builder.Property(e => e.CollectorCode).HasMaxLength(20);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(150);
        builder.Property(e => e.RequestSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.PaymentMeansClass).HasMaxLength(30);
        builder.Property(e => e.ReceiptNumber).HasMaxLength(40);
        builder.Property(e => e.ReceiptMeansReference).HasMaxLength(80);
        builder.Property(e => e.SignedByPersonIdsJson).HasMaxLength(400);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.RulesVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PrevalidationOutcome).HasMaxLength(20);

        builder.HasOne(e => e.MemberAccount).WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.CounterpartMemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AuthorizedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.AuthorizationDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.RelatedLoanOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingInboundMessage>().WithMany().HasForeignKey(e => e.InboundMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);
        // Nombres propios: los generados pasan de 63 caracteres y PostgreSQL los truncaría al mismo prefijo.
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.ReversesOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountOperations_Reverses");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.ReversedByOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountOperations_ReversedBy");

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_MemberAccountOperations_Number");
        builder.HasIndex(e => new { e.MemberAccountId, e.Sequence }).IsUnique().HasDatabaseName("UK_LND_MemberAccountOperations_Account_Sequence");
        builder.HasIndex(e => new { e.CollectorCode, e.IdempotencyKey }).IsUnique()
            .HasDatabaseName("UK_LND_MemberAccountOperations_Collector_Key").HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(e => e.ReversesOperationId).IsUnique()
            .HasDatabaseName("UK_LND_MemberAccountOperations_Reverses").HasFilter("[ReversesOperationId] IS NOT NULL");
        builder.HasIndex(e => new { e.MemberAccountId, e.ValueDate }).HasDatabaseName("IX_LND_MemberAccountOperations_Account_ValueDate");
        builder.HasIndex(e => e.CounterpartMemberAccountId).HasDatabaseName("IX_LND_MemberAccountOperations_Counterpart")
            .HasFilter("[CounterpartMemberAccountId] IS NOT NULL");
        builder.HasIndex(e => e.RelatedLoanOperationId).HasDatabaseName("IX_LND_MemberAccountOperations_RelatedLoanOperation")
            .HasFilter("[RelatedLoanOperationId] IS NOT NULL");
        builder.HasIndex(e => e.ClearingUntil).HasDatabaseName("IX_LND_MemberAccountOperations_Clearing")
            .HasFilter("[ClearedAt] IS NULL AND [ClearingUntil] IS NOT NULL");
        // US10 (T096, research E17): la FK nace con LND_MembershipWithdrawals.
        builder.HasOne<Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawal>().WithMany().HasForeignKey(e => e.MembershipWithdrawalId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_LND_MemberAccountOperations_MembershipWithdrawal");
        builder.HasIndex(e => e.MembershipWithdrawalId).HasDatabaseName("IX_LND_MemberAccountOperations_MembershipWithdrawal")
            .HasFilter("[MembershipWithdrawalId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_MemberAccountTransactions</c> (feature 014, K7; data-model §4.2): el libro de cuentas, hecho <c>bigint</c>. Exactamente un origen
/// (operación o corrida), importe distinto de cero con hasta 6 decimales (<c>MontoCausado</c>), una causación por cuenta, concepto y día (la
/// idempotencia del proceso) y un espejo por fila.
/// </summary>
public class MemberAccountTransactionConfiguration : IEntityTypeConfiguration<MemberAccountTransaction>
{
    public const string Tabla = "LND_MemberAccountTransactions";

    public void Configure(EntityTypeBuilder<MemberAccountTransaction> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_MemberAccountTransactions_Origin",
                "([OperationId] IS NOT NULL AND [ProcessRunId] IS NULL) OR ([OperationId] IS NULL AND [ProcessRunId] IS NOT NULL)");
            t.HasCheckConstraint("CK_LND_MemberAccountTransactions_Amount", "[Amount] <> 0");
        });

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Concept).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).MontoCausado().IsRequired();
        builder.Property(e => e.ValueDate).IsRequired();
        builder.Property(e => e.AccountingDate).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();

        builder.HasOne(e => e.MemberAccount).WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccountTransaction>().WithMany().HasForeignKey(e => e.ReversesTransactionId).OnDelete(DeleteBehavior.Restrict);
        // US3 (T081, research E17): el pago de una cuota exigible nombra su cuota.
        builder.HasOne<AccountInstallment>().WithMany().HasForeignKey(e => e.InstallmentId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTransactions_Installment");

        // La causación de una cuenta, un concepto y un día ocurre una vez (InterestAccrued = 6), salvo sus espejos.
        builder.HasIndex(e => new { e.MemberAccountId, e.Concept, e.ValueDate }).IsUnique()
            .HasDatabaseName("UK_LND_MemberAccountTransactions_Accrual")
            .HasFilter("[Kind] = 6 AND [ReversesTransactionId] IS NULL");
        builder.HasIndex(e => e.ReversesTransactionId).IsUnique()
            .HasDatabaseName("UK_LND_MemberAccountTransactions_Reverses").HasFilter("[ReversesTransactionId] IS NOT NULL");

        builder.HasIndex(e => new { e.MemberAccountId, e.ValueDate, e.Id }).HasDatabaseName("IX_LND_MemberAccountTransactions_Account_ValueDate");
        builder.HasIndex(e => new { e.MemberAccountId, e.AccountingDate }).HasDatabaseName("IX_LND_MemberAccountTransactions_Account_AccDate");
        builder.HasIndex(e => e.AccountingDate).HasDatabaseName("IX_LND_MemberAccountTransactions_AccountingDate");
        builder.HasIndex(e => e.OperationId).HasDatabaseName("IX_LND_MemberAccountTransactions_OperationId").HasFilter("[OperationId] IS NOT NULL");
        builder.HasIndex(e => e.ProcessRunId).HasDatabaseName("IX_LND_MemberAccountTransactions_ProcessRunId").HasFilter("[ProcessRunId] IS NOT NULL");
        builder.HasIndex(e => e.InstallmentId).HasDatabaseName("IX_LND_MemberAccountTransactions_InstallmentId").HasFilter("[InstallmentId] IS NOT NULL");
        builder.HasIndex(e => e.MessagePublicId).HasDatabaseName("IX_LND_MemberAccountTransactions_MessagePublicId")
            .HasFilter("[MessagePublicId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_MemberAccountDailyPositions</c> (feature 014, K7; data-model §4.3): instantánea versionada <c>bigint</c>; único <c>(MemberAccountId,
/// PositionDate, Version)</c> sin filtro e índices de listados por fecha, oficina y línea, y por fecha y naturaleza.
/// </summary>
public class MemberAccountDailyPositionConfiguration : IEntityTypeConfiguration<MemberAccountDailyPosition>
{
    public const string Tabla = "LND_MemberAccountDailyPositions";

    public void Configure(EntityTypeBuilder<MemberAccountDailyPosition> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.PositionDate).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Nature).HasConversion<int>().IsRequired();
        builder.Property(e => e.DepositClass).HasConversion<int?>();
        builder.Property(e => e.IssueTermBand).HasConversion<int?>();

        builder.Property(e => e.Principal).Monto().IsRequired();
        builder.Property(e => e.ContributionMinimum).Monto().IsRequired();
        builder.Property(e => e.ContributionOrdinary).Monto().IsRequired();
        builder.Property(e => e.ContributionExtraordinary).Monto().IsRequired();
        builder.Property(e => e.ContributionRevaluation).Monto().IsRequired();
        builder.Property(e => e.Bonus).Monto().IsRequired();
        builder.Property(e => e.ChargeReceivable).Monto().IsRequired();
        builder.Property(e => e.InterestPayable).MontoCausado().IsRequired();
        builder.Property(e => e.HeldAmount).Monto().IsRequired();
        builder.Property(e => e.GarnishedAmount).Monto().IsRequired();
        builder.Property(e => e.ClearingAmount).Monto().IsRequired();
        builder.Property(e => e.MinimumBalance).Monto().IsRequired();
        builder.Property(e => e.AvailableBalance).Monto().IsRequired();
        builder.Property(e => e.WithholdingYearToDate).Monto().IsRequired();
        builder.Property(e => e.GmfMonthToDate).Monto().IsRequired();
        builder.Property(e => e.AppliedAnnualRate).TasaAnual();
        builder.Property(e => e.InstallmentsInArrears).IsRequired();
        builder.Property(e => e.ArrearsAmount).Monto().IsRequired();
        builder.Property(e => e.IsInactive).IsRequired();
        builder.Property(e => e.IsGarnished).IsRequired();
        builder.Property(e => e.IsGmfExempt).IsRequired();
        builder.Property(e => e.IsAccrualSuspended).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();

        builder.HasOne(e => e.MemberAccount).WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MemberAccountId, e.PositionDate, e.Version }).IsUnique()
            .HasDatabaseName("UK_LND_MemberAccountDailyPositions_Account_Date_Version");
        builder.HasIndex(e => new { e.PositionDate, e.BranchId, e.CreditLineId }).HasDatabaseName("IX_LND_MemberAccountDailyPositions_Date_Branch_Line");
        builder.HasIndex(e => new { e.PositionDate, e.Nature }).HasDatabaseName("IX_LND_MemberAccountDailyPositions_Date_Nature");
    }
}

/// <summary><c>LND_MemberAccountControls</c> (feature 014, K7; data-model §4.4): una fila por cuenta (único sin filtro), cerrojo y marcas de agua.</summary>
public class MemberAccountControlConfiguration : IEntityTypeConfiguration<MemberAccountControl>
{
    public const string Tabla = "LND_MemberAccountControls";

    public void Configure(EntityTypeBuilder<MemberAccountControl> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.LastAccruedDate).IsRequired();
        builder.Property(e => e.Sequence).IsRequired();
        builder.Property(e => e.LastRegisteredAt).IsRequired();
        builder.Property(e => e.LastInstallmentPeriod).HasMaxLength(10);

        builder.HasOne(e => e.MemberAccount).WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MemberAccountId).IsUnique().HasDatabaseName("UK_LND_MemberAccountControls_MemberAccountId");
    }
}
