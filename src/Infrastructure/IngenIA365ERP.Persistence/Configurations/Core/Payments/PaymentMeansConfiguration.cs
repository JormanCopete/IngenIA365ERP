using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Payments;

/// <summary>
/// <c>COR_PaymentMeans</c> (feature 012, I3, T582; FR-096; data-model §16): código único entre vivos; enums como int; tolerancia y
/// comisión fija en pesos (18,2) y la tasa de comisión como fracción (9,6); FK <c>Restrict</c> a red, adquirente y banco. Entra en
/// el par <c>VentasYPuntoDeVenta</c>.
/// </summary>
public class PaymentMeansConfiguration : IEntityTypeConfiguration<PaymentMeans>
{
    public void Configure(EntityTypeBuilder<PaymentMeans> builder)
    {
        builder.ComoEntidadDeInventario("COR_PaymentMeans");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_COR_PaymentMeans_Code").HasFilter("[IsDeleted] = 0");
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.QuickKey).HasMaxLength(3);
        builder.Property(e => e.Class).IsRequired();
        builder.Property(e => e.DestinationAccountNumber).HasMaxLength(25);
        builder.Property(e => e.CountMethod).IsRequired();
        builder.Property(e => e.ToleranceAmount).Monto().HasDefaultValue(0m).IsRequired();
        builder.Property(e => e.ExpectedCommissionRate).Tarifa();
        builder.Property(e => e.ExpectedCommissionFixed).Monto();
        builder.Property(e => e.DianPaymentMeansCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SuggestedCreditLineCode).HasMaxLength(20);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.Notes).HasMaxLength(300);

        builder.HasOne(e => e.CardNetwork).WithMany().HasForeignKey(e => e.CardNetworkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CardAcquirer).WithMany().HasForeignKey(e => e.CardAcquirerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
    }
}
