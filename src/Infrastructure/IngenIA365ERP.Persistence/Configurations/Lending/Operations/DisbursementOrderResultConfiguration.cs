using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementOrderResults</c> (feature 013; data-model §7.5): hecho; un resultado por mensaje consumido (único) e índice por
/// orden en el orden en que se registraron.
/// </summary>
public class DisbursementOrderResultConfiguration : IEntityTypeConfiguration<DisbursementOrderResult>
{
    public const string Tabla = "LND_DisbursementOrderResults";

    public void Configure(EntityTypeBuilder<DisbursementOrderResult> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Result).HasConversion<int>().IsRequired();
        builder.Property(e => e.ResultDate).IsRequired();
        builder.Property(e => e.TreasuryReference).HasMaxLength(60);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.RegisteredAt).IsRequired();

        builder.HasOne<DisbursementOrder>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingInboundMessage>().WithMany().HasForeignKey(e => e.InboundMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.ReversalOperationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.InboundMessageId).IsUnique().HasDatabaseName("UK_LND_DisbursementOrderResults_Message");
        builder.HasIndex(e => new { e.OrderId, e.RegisteredAt }).HasDatabaseName("IX_LND_DisbursementOrderResults_Order_RegisteredAt");
    }
}
