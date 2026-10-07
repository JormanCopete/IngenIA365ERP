using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Integration;

/// <summary>
/// <c>LND_InboundMessages</c> (feature 013, C.3; data-model §21.1): recibo único por <c>MessagePublicId</c> (sin filtro: un
/// <c>Rejected</c> se reprocesa en la misma fila). Sin <c>PayrollDeductionFileId</c> hasta K2.
/// </summary>
public class LendingInboundMessageConfiguration : IEntityTypeConfiguration<LendingInboundMessage>
{
    public const string Tabla = "LND_InboundMessages";

    public void Configure(EntityTypeBuilder<LendingInboundMessage> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.MessagePublicId).IsRequired();
        builder.Property(e => e.MessageType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.MessageVersion).IsRequired();
        builder.Property(e => e.OriginModule).HasMaxLength(3).IsRequired();
        builder.Property(e => e.OriginPublicId).IsRequired();
        builder.Property(e => e.OriginNumber).HasMaxLength(30);
        builder.Property(e => e.OriginEventKey).HasMaxLength(80).IsRequired();
        builder.Property(e => e.ChainRootPublicId).IsRequired();
        builder.Property(e => e.OperationDate).IsRequired();
        builder.Property(e => e.ReservedLoanNumber).HasMaxLength(30);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.EvaluationCount).IsRequired();
        builder.Property(e => e.NotRetryableReason).HasMaxLength(500);
        builder.Property(e => e.FirstReceivedAt).IsRequired();
        builder.Property(e => e.LastProcessedAt).IsRequired();
        builder.Property(e => e.ExceptionReason).HasMaxLength(1000);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DisbursementOrder>().WithMany().HasForeignKey(e => e.DisbursementOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationSaleReconciliation>().WithMany().HasForeignKey(e => e.SaleReconciliationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingInboundMessageEvaluation>().WithMany().HasForeignKey(e => e.LastEvaluationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.ExceptionDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.NotRetryableByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ConfirmedByExceptionByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName("UK_LND_InboundMessages_MessagePublicId");
        builder.HasIndex(e => e.CreditPaymentPublicId).HasDatabaseName("IX_LND_InboundMessages_CreditPaymentPublicId")
            .HasFilter("[CreditPaymentPublicId] IS NOT NULL");
        builder.HasIndex(e => e.OriginPublicId).HasDatabaseName("IX_LND_InboundMessages_OriginPublicId");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_InboundMessages_LoanId");
        builder.HasIndex(e => new { e.Status, e.LastProcessedAt }).HasDatabaseName("IX_LND_InboundMessages_Status_LastProcessedAt");
    }
}
