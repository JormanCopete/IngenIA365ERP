using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Documents;

/// <summary>
/// <c>INV_LendingObligationResponses</c> (feature 013, T0174; data-model parte 3 §8.3): el recibo de las respuestas de Cartera a una
/// venta a crédito. <c>UK_INV_LendingObligationResponses_Message</c> sin filtro (recibo: una colisión es <c>AlreadyProcessed</c>) e
/// <c>IX_INV_LendingObligationResponses_Payment (DocumentPaymentId, ReceivedAt)</c> para tomar la última. Sin FK a <c>LND_</c>: la
/// obligación es un <c>PublicId</c> y un número (FR-008, C34). (nuevo)
/// </summary>
public class LendingObligationResponseConfiguration : IEntityTypeConfiguration<LendingObligationResponse>
{
    public void Configure(EntityTypeBuilder<LendingObligationResponse> builder)
    {
        builder.ComoEntidadDeInventario("INV_LendingObligationResponses");

        builder.Property(e => e.MessageType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.MessageVersion).IsRequired();
        builder.Property(e => e.Outcome).IsRequired();
        builder.Property(e => e.ObligationNumber).HasMaxLength(30);
        builder.Property(e => e.ReasonCode).HasMaxLength(60);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.Retryable).IsRequired();
        builder.Property(e => e.ReceivedAt).IsRequired();

        builder.HasOne(e => e.DocumentPayment).WithMany().HasForeignKey(e => e.DocumentPaymentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName("UK_INV_LendingObligationResponses_Message");
        builder.HasIndex(e => new { e.DocumentPaymentId, e.ReceivedAt }).HasDatabaseName("IX_INV_LendingObligationResponses_Payment");
    }
}
