using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

/// <summary>
/// <c>LND_MembershipWithdrawals</c> (feature 014, US10, T096; data-model §6.1): número único, una solicitud radicada y una aprobada por persona (dos
/// únicos filtrados: los filtros no admiten <c>IN</c> ni <c>OR</c>, 013 §0.13) y el índice por estado. Migración <c>CaptacionesAportes</c>.
/// </summary>
public class MembershipWithdrawalConfiguration : IEntityTypeConfiguration<MembershipWithdrawal>
{
    public const string Tabla = "LND_MembershipWithdrawals";

    public void Configure(EntityTypeBuilder<MembershipWithdrawal> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_MembershipWithdrawals_Refund", "[RefundAmount] IS NULL OR [RefundAmount] >= 0"));

        builder.Property(e => e.Number).HasMaxLength(MembershipWithdrawal.LargoDelNumero).IsRequired();
        builder.Property(e => e.Cause).HasConversion<int>().IsRequired();
        builder.Property(e => e.CauseDetail).HasMaxLength(MembershipWithdrawal.LargoDelDetalle);
        builder.Property(e => e.SupportAttachmentPublicIdsJson).IsRequired();
        builder.Property(e => e.FiledOn).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.DecisionReason).HasMaxLength(MembershipWithdrawal.LargoDelDetalle);
        builder.Property(e => e.DesistReason).HasMaxLength(MembershipWithdrawal.LargoDelMotivoDeDesistimiento);
        builder.Property(e => e.ToRedoReason).HasMaxLength(MembershipWithdrawal.LargoDelDetalle);
        // Decisión del dueño del 2026-10-18 (D45): qué pasa con lo que los créditos siguen debiendo después del cruce.
        builder.Property(e => e.PendingLoanBalanceHandling).HasConversion<int?>();
        builder.Property(e => e.ContributionsTotal).Monto();
        builder.Property(e => e.SavingsTotal).Monto();
        builder.Property(e => e.LoansOffsetTotal).Monto();
        builder.Property(e => e.LossDeductionAmount).Monto();
        builder.Property(e => e.RefundAmount).Monto();

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        // Nombres propios donde los generados pasan de 63 caracteres (PostgreSQL los trunca; research E18).
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.ContributionAccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawals_ContributionAccount");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.FiledByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SettledByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.ApprovalSessionItemId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawals_ApprovalSessionItem");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.OffsetOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawals_OffsetOperation");
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.RefundDecisionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawals_RefundDecision");
        builder.HasOne<IssuedLendingDocument>().WithMany().HasForeignKey(e => e.SettlementIssuedDocumentId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawals_SettlementDocument");

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_MembershipWithdrawals_Number");
        // Dos únicos sobre la misma columna: índices con nombre (EF junta los anónimos de las mismas columnas en uno).
        builder.HasIndex(e => e.PersonId, "UK_LND_MembershipWithdrawals_Filed").IsUnique().HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.PersonId, "UK_LND_MembershipWithdrawals_Approved").IsUnique().HasFilter("[Status] = 2 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_LND_MembershipWithdrawals_Status");
    }
}

/// <summary>
/// <c>LND_MembershipWithdrawalLines</c> (feature 014, US10, T096; data-model §6.2): hecho, único por retiro, versión y número de línea (sin filtro de
/// baja: un hecho nunca se da de baja, research E18).
/// </summary>
public class MembershipWithdrawalLineConfiguration : IEntityTypeConfiguration<MembershipWithdrawalLine>
{
    public const string Tabla = "LND_MembershipWithdrawalLines";

    public void Configure(EntityTypeBuilder<MembershipWithdrawalLine> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Concept).HasMaxLength(MembershipWithdrawalLine.LargoDelConcepto);
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(MembershipWithdrawalLine.LargoDeLaNota);

        builder.HasOne<MembershipWithdrawal>().WithMany().HasForeignKey(e => e.MembershipWithdrawalId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawalLines_Withdrawal");
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawalLines_Account");
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.AccountOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawalLines_AccountOperation");
        builder.HasOne<Domain.Entities.Lending.Operations.LoanOperation>().WithMany().HasForeignKey(e => e.LoanOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MembershipWithdrawalLines_LoanOperation");

        builder.HasIndex(e => new { e.MembershipWithdrawalId, e.SettlementVersion, e.LineNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MembershipWithdrawalLines_Version_Line");
        builder.HasIndex(e => e.MemberAccountId).HasDatabaseName("IX_LND_MembershipWithdrawalLines_Account").HasFilter("[MemberAccountId] IS NOT NULL");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_MembershipWithdrawalLines_Loan").HasFilter("[LoanId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_AssemblyDistributions</c> (feature 014, US12, T202; data-model §6.3; research §22): número único, una aplicada por acta y clase (único filtrado a
/// <c>Applied</c>: la reversada no estorba a su corrección) y el índice por estado. Migración <c>CaptacionesAportes</c>.
/// </summary>
public class AssemblyDistributionConfiguration : IEntityTypeConfiguration<AssemblyDistribution>
{
    public const string Tabla = "LND_AssemblyDistributions";

    public void Configure(EntityTypeBuilder<AssemblyDistribution> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_AssemblyDistributions_Fund", "[FundAmount] >= 0");
            t.HasCheckConstraint("CK_LND_AssemblyDistributions_Decision",
                "([DecidedFraction] IS NULL AND [DecidedValue] IS NOT NULL) OR ([DecidedFraction] IS NOT NULL AND [DecidedValue] IS NULL)");
        });

        builder.Property(e => e.Number).HasMaxLength(AssemblyDistribution.LargoDelNumero).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.BaseMethod).HasConversion<int>().IsRequired();
        builder.Property(e => e.DecidedFraction).Fraccion();
        builder.Property(e => e.DecidedValue).Monto();
        builder.Property(e => e.LegalLimitFraction).Fraccion();
        builder.Property(e => e.LegalLimitSource).HasMaxLength(AssemblyDistribution.LargoDeLaFuente);
        builder.Property(e => e.FundAmount).Monto().IsRequired();
        builder.Property(e => e.CalculatedTotal).Monto().IsRequired();
        builder.Property(e => e.ProrationFactor).Fraccion();
        builder.Property(e => e.AppliedTotal).Monto().IsRequired();
        builder.Property(e => e.SurplusDestination).HasConversion<int?>();
        builder.Property(e => e.RoundingUnit).Monto().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(AssemblyDistribution.LargoDelMotivo).IsRequired();
        builder.Property(e => e.ReversalReason).HasMaxLength(AssemblyDistribution.LargoDelMotivo);
        builder.Property(e => e.VoidReason).HasMaxLength(AssemblyDistribution.LargoDelMotivo);
        builder.Property(e => e.SupportAttachmentPublicIdsJson).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(64).IsRequired();

        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.DecisionSessionItemId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributions_SessionItem");
        builder.HasOne<AssemblyDistribution>().WithMany().HasForeignKey(e => e.CorrectsDistributionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributions_Corrects");
        builder.HasOne<Domain.Entities.Lending.Configuration.CreditLine>().WithMany().HasForeignKey(e => e.DestinationLineId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributions_DestinationLine");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributions_RegisteredBy");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributions_ApprovedBy");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReversedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributions_ReversedBy");

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_AssemblyDistributions_Number");
        builder.HasIndex(e => new { e.DecisionSessionItemId, e.Kind }, "UK_LND_AssemblyDistributions_Session").IsUnique()
            .HasFilter("[Status] = 3 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.FiscalYear, e.Status }).HasDatabaseName("IX_LND_AssemblyDistributions_Year_Status");
    }
}

/// <summary>
/// <c>LND_AssemblyDistributionLines</c> (feature 014, US12, T202; data-model §6.4): hecho, único por distribución, versión y persona (sin filtro de baja,
/// research E18); una línea por operación.
/// </summary>
public class AssemblyDistributionLineConfiguration : IEntityTypeConfiguration<AssemblyDistributionLine>
{
    public const string Tabla = "LND_AssemblyDistributionLines";

    public void Configure(EntityTypeBuilder<AssemblyDistributionLine> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.BaseAmount).Monto().IsRequired();
        builder.Property(e => e.CalculatedAmount).Monto().IsRequired();
        builder.Property(e => e.ProratedAmount).Monto().IsRequired();
        builder.Property(e => e.WithdrawnDuringYear).HasDefaultValue(false);
        builder.Property(e => e.WithheldAmount).Monto().HasDefaultValue(0m);
        builder.Property(e => e.WithholdingRate).Fraccion();
        builder.Property(e => e.WithholdingConcept).HasMaxLength(60);

        builder.HasOne<AssemblyDistribution>().WithMany().HasForeignKey(e => e.AssemblyDistributionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributionLines_Distribution");
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributionLines_Person");
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.ContributionAccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributionLines_ContributionAccount");
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.DestinationAccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributionLines_DestinationAccount");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributionLines_Operation");
        builder.HasOne<AccountPayoutOrder>().WithMany().HasForeignKey(e => e.PayoutOrderId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AssemblyDistributionLines_PayoutOrder");

        builder.HasIndex(e => new { e.AssemblyDistributionId, e.SimulationVersion, e.PersonId }).IsUnique()
            .HasDatabaseName("UK_LND_AssemblyDistributionLines_Version_Person");
        builder.HasIndex(e => e.OperationId).IsUnique().HasDatabaseName("UK_LND_AssemblyDistributionLines_Operation").HasFilter("[OperationId] IS NOT NULL");
        builder.HasIndex(e => e.ContributionAccountId).HasDatabaseName("IX_LND_AssemblyDistributionLines_ContributionAccount");
    }
}

/// <summary>
/// <c>LND_DeceasedHolderCases</c> (feature 014, US14, T207; data-model §6.5; research §23): número único, un caso no anulado por titular (único filtrado) y el
/// índice por estado. Migración <c>CaptacionesAportes</c>.
/// </summary>
public class DeceasedHolderCaseConfiguration : IEntityTypeConfiguration<DeceasedHolderCase>
{
    public const string Tabla = "LND_DeceasedHolderCases";

    public void Configure(EntityTypeBuilder<DeceasedHolderCase> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_DeceasedHolderCases_Total", "[TotalAmount] >= 0");
            t.HasCheckConstraint("CK_LND_DeceasedHolderCases_Dates", "[DeathDate] <= [ReportedOn]");
        });

        builder.Property(e => e.Number).HasMaxLength(DeceasedHolderCase.LargoDelNumero).IsRequired();
        builder.Property(e => e.Route).HasConversion<int?>();
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.WithoutProbateCap).Monto();
        builder.Property(e => e.SupportAttachmentPublicIdsJson).IsRequired();
        builder.Property(e => e.PreviewSha256).HasMaxLength(64);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(DeceasedHolderCase.LargoDelMotivo).IsRequired();
        builder.Property(e => e.CancelReason).HasMaxLength(DeceasedHolderCase.LargoDelMotivo);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DeceasedHolderCases_Person");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DeceasedHolderCases_RegisteredBy");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DeliveryRegisteredByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DeceasedHolderCases_DeliveryRegisteredBy");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DeceasedHolderCases_ApprovedBy");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CancelledByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DeceasedHolderCases_CancelledBy");

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_DeceasedHolderCases_Number");
        builder.HasIndex(e => e.PersonId, "UK_LND_DeceasedHolderCases_Person").IsUnique().HasFilter("[Status] <> 4 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_LND_DeceasedHolderCases_Status");
    }
}

/// <summary>
/// <c>LND_UnclaimedBalances</c> (feature 014, US14, T207; data-model §6.6; research §23): un traslado por operación, el índice por persona y estado y el de la
/// cuenta de origen. Migración <c>CaptacionesAportes</c>.
/// </summary>
public class UnclaimedBalanceConfiguration : IEntityTypeConfiguration<UnclaimedBalance>
{
    public const string Tabla = "LND_UnclaimedBalances";

    public void Configure(EntityTypeBuilder<UnclaimedBalance> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_UnclaimedBalances_Amount", "[Amount] > 0"));

        builder.Property(e => e.Source).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(UnclaimedBalance.LargoDelMotivo).IsRequired();

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_UnclaimedBalances_Person");
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.SourceMemberAccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_UnclaimedBalances_SourceAccount");
        builder.HasOne<AccountPayoutOrder>().WithMany().HasForeignKey(e => e.SourcePayoutOrderId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_UnclaimedBalances_SourcePayoutOrder");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.TransferOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_UnclaimedBalances_TransferOperation");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.ClaimOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_UnclaimedBalances_ClaimOperation");
        builder.HasOne<AccountPayoutOrder>().WithMany().HasForeignKey(e => e.ClaimPayoutOrderId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_UnclaimedBalances_ClaimPayoutOrder");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.TransferredByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_UnclaimedBalances_TransferredBy");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClaimedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_UnclaimedBalances_ClaimedBy");

        builder.HasIndex(e => e.TransferOperationId).IsUnique().HasDatabaseName("UK_LND_UnclaimedBalances_TransferOperation");
        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName("IX_LND_UnclaimedBalances_Person_Status");
        builder.HasIndex(e => e.SourceMemberAccountId).HasDatabaseName("IX_LND_UnclaimedBalances_SourceAccount");
        builder.HasIndex(e => e.ClaimPayoutOrderId).HasDatabaseName("IX_LND_UnclaimedBalances_ClaimPayoutOrder").HasFilter("[ClaimPayoutOrderId] IS NOT NULL");
    }
}
