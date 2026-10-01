using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Documents;

/// <summary>
/// <c>INV_DocumentPayments</c> (feature 012, I3, T584; FR-097, FR-101; data-model §16): único <c>(DocumentId, LineNumber)</c> entre
/// vivos; importes en pesos; <c>Last4</c> es <c>char(4)</c> —nunca el número de la tarjeta
/// (<c>LosPagosNoGuardanElNumeroDeTarjeta</c>)—. Índices del arqueo <c>(CashSessionId, PaymentMeansId)</c>, de los lotes
/// <c>(CardTerminalId, TerminalBatchNumber)</c> y de la búsqueda <c>(PaymentMeansId, NormalizedReference)</c>. Las copias del
/// medio (<c>BankId</c>, <c>CardAcquirerPersonId</c>) no llevan FK: son copias.
/// </summary>
public class DocumentPaymentConfiguration : IEntityTypeConfiguration<DocumentPayment>
{
    public void Configure(EntityTypeBuilder<DocumentPayment> builder)
    {
        builder.ComoEntidadDeInventario("INV_DocumentPayments");

        builder.Property(e => e.Direction).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.AmountTendered).Monto();
        builder.Property(e => e.ChangeGiven).Monto();
        builder.Property(e => e.Reference).HasMaxLength(40);
        builder.Property(e => e.NormalizedReference).HasMaxLength(40);
        builder.Property(e => e.AuthorizationCode).HasMaxLength(20);
        builder.Property(e => e.TerminalBatchNumber).HasMaxLength(20);
        builder.Property(e => e.Last4).HasMaxLength(4).IsFixedLength();
        builder.Property(e => e.MeansCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.MeansName).HasMaxLength(60).IsRequired();
        builder.Property(e => e.MeansClass).IsRequired();
        builder.Property(e => e.CardNetworkCode).HasMaxLength(10);
        builder.Property(e => e.CardAcquirerCode).HasMaxLength(10);
        builder.Property(e => e.DianPaymentMeansCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ExpectedCommissionAmount).Monto();
        builder.Property(e => e.SuggestedCreditLineCode).HasMaxLength(20);
        builder.Property(e => e.PendingValidation).HasDefaultValue(false);
        builder.Property(e => e.AccountsReceivableRecordedBy).HasMaxLength(12);

        builder.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PaymentMeans).WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CardTerminal>().WithMany().HasForeignKey(e => e.CardTerminalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentPayment>().WithMany().HasForeignKey(e => e.RefundsPaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalRequest>().WithMany().HasForeignKey(e => e.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentId, e.LineNumber }).IsUnique()
            .HasDatabaseName("UK_INV_DocumentPayments_Document_LineNumber").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.CashSessionId, e.PaymentMeansId }).HasDatabaseName("IX_INV_DocumentPayments_Session_Means");
        builder.HasIndex(e => new { e.CardTerminalId, e.TerminalBatchNumber }).HasDatabaseName("IX_INV_DocumentPayments_Terminal_Batch");
        builder.HasIndex(e => new { e.PaymentMeansId, e.NormalizedReference }).HasDatabaseName("IX_INV_DocumentPayments_Means_Reference")
            .HasFilter("[NormalizedReference] IS NOT NULL");
    }
}
