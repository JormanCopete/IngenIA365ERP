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
