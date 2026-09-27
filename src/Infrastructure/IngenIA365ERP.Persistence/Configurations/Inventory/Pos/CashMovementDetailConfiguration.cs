using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashMovementDetails</c> (feature 012, I3, T583; FR-100; data-model §15): 1:1 con su documento <c>CashMovement</c>
/// (único <c>DocumentId</c> entre vivos); valor en pesos; FK <c>Restrict</c> a sesiones, medios, caja destino, banco, pago
/// reclasificado y datáfono.
/// </summary>
public class CashMovementDetailConfiguration : IEntityTypeConfiguration<CashMovementDetail>
{
    public void Configure(EntityTypeBuilder<CashMovementDetail> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashMovementDetails");

        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.TargetReference).HasMaxLength(40);
        builder.Property(e => e.TargetAuthorizationCode).HasMaxLength(20);

        builder.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>().WithMany().HasForeignKey(e => e.DestinationCashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashRegister>().WithMany().HasForeignKey(e => e.DestinationCashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.SourcePaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.TargetPaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.DepositBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentPayment>().WithMany().HasForeignKey(e => e.ReclassifiedPaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CardTerminal>().WithMany().HasForeignKey(e => e.TargetCardTerminalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.DocumentId).IsUnique().HasDatabaseName("UK_INV_CashMovementDetails_DocumentId").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.CashSessionId).HasDatabaseName("IX_INV_CashMovementDetails_CashSessionId");
        builder.HasIndex(e => e.DestinationCashSessionId).HasDatabaseName("IX_INV_CashMovementDetails_DestinationCashSessionId")
            .HasFilter("[DestinationCashSessionId] IS NOT NULL");
    }
}
