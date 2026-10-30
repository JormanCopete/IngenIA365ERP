using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

/// <summary>
/// <c>LND_LoanOperations</c> (feature 013; data-model §7.1): hecho <c>bigint</c>, comprobante único, secuencia única por crédito,
/// idempotencia por recaudador y clave (T21 capa 2) y un reverso por operación. Las clases de recaudador, de medio y la validación
/// previa viajan como texto.
/// </summary>
public class LoanOperationConfiguration : IEntityTypeConfiguration<LoanOperation>
{
    public const string Tabla = "LND_LoanOperations";

    public void Configure(EntityTypeBuilder<LoanOperation> builder)
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
        builder.Property(e => e.PaymentModality).HasConversion<int?>();
        builder.Property(e => e.PrepaymentChoice).HasConversion<int?>();
        builder.Property(e => e.PaymentMeansClass).HasMaxLength(30);
        builder.Property(e => e.ReceiptNumber).HasMaxLength(40);
        builder.Property(e => e.ReceiptMeansReference).HasMaxLength(80);
        builder.Property(e => e.PaymasterPendingLineReference).HasMaxLength(80);
        // Feature 015, R1 (E-01 a; CarteraYCaptacionesConPartidaCitada): la partida citada por la v2, con índice no único.
        builder.HasIndex(e => e.BankItemPublicId).HasDatabaseName("IX_LND_LoanOperations_BankItemPublicId");
        builder.Property(e => e.SimulationId).HasMaxLength(64);
        builder.Property(e => e.ExcessAmount).Monto();

        builder.Property(e => e.DisbursementForm).HasConversion<int?>();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.RulesVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PrevalidationOutcome).HasMaxLength(20);

        builder.HasOne(e => e.Loan).WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PayerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.ReversesOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.ReversedByOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.RelatedOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingInboundMessage>().WithMany().HasForeignKey(e => e.InboundMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InsuranceClaim>().WithMany().HasForeignKey(e => e.InsuranceClaimId).OnDelete(DeleteBehavior.Restrict);
        // K2 (CarteraLibranza): el pago de la pagaduría nombra el resultado «pendiente de giro» que gira.
        builder.HasOne<PayrollDeductionResult>().WithMany().HasForeignKey(e => e.PayrollDeductionResultId).OnDelete(DeleteBehavior.Restrict);
        // K3 (CarteraNovedades): la novedad, la condonación o el ítem del lote de castigo que la operación aplica.
        builder.HasOne<LoanNovelty>().WithMany().HasForeignKey(e => e.NoveltyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Waiver>().WithMany().HasForeignKey(e => e.WaiverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WriteOffBatchItem>().WithMany().HasForeignKey(e => e.WriteOffBatchItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_LoanOperations_Number");
        builder.HasIndex(e => new { e.LoanId, e.Sequence }).IsUnique().HasDatabaseName("UK_LND_LoanOperations_Loan_Sequence");
        builder.HasIndex(e => new { e.CollectorCode, e.IdempotencyKey }).IsUnique()
            .HasDatabaseName("UK_LND_LoanOperations_Collector_Key").HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(e => e.ReversesOperationId).IsUnique()
            .HasDatabaseName("UK_LND_LoanOperations_Reverses").HasFilter("[ReversesOperationId] IS NOT NULL");
        builder.HasIndex(e => new { e.LoanId, e.ValueDate }).HasDatabaseName("IX_LND_LoanOperations_Loan_ValueDate");
        builder.HasIndex(e => e.PayerPersonId).HasDatabaseName("IX_LND_LoanOperations_PayerPersonId");
        builder.HasIndex(e => e.RelatedOperationId)
            .HasDatabaseName("IX_LND_LoanOperations_RelatedOperationId").HasFilter("[RelatedOperationId] IS NOT NULL");
        builder.HasIndex(e => e.PayrollDeductionResultId)
            .HasDatabaseName("IX_LND_LoanOperations_PayrollDeductionResultId").HasFilter("[PayrollDeductionResultId] IS NOT NULL");
        builder.HasIndex(e => e.NoveltyId).HasDatabaseName("IX_LND_LoanOperations_NoveltyId").HasFilter("[NoveltyId] IS NOT NULL");
        builder.HasIndex(e => e.WaiverId).HasDatabaseName("IX_LND_LoanOperations_WaiverId").HasFilter("[WaiverId] IS NOT NULL");
        builder.HasIndex(e => e.WriteOffBatchItemId)
            .HasDatabaseName("IX_LND_LoanOperations_WriteOffBatchItemId").HasFilter("[WriteOffBatchItemId] IS NOT NULL");
    }
}
