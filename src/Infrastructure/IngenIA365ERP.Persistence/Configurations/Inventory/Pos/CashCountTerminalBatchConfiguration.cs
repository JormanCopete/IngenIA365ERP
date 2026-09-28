using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashCountTerminalBatches</c> (feature 012, I3, T583; FR-101; data-model §15): único
/// <c>(CashCountLineId, CardTerminalId, BatchNumber)</c> entre vivos; totales en pesos.
/// </summary>
public class CashCountTerminalBatchConfiguration : IEntityTypeConfiguration<CashCountTerminalBatch>
{
    public void Configure(EntityTypeBuilder<CashCountTerminalBatch> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashCountTerminalBatches");

        builder.Property(e => e.BatchNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BatchTotal).Monto().IsRequired();
        builder.Property(e => e.ExpectedTotal).Monto().IsRequired();

        builder.HasOne<CashCountLine>().WithMany(l => l.TerminalBatches).HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CardTerminal>().WithMany().HasForeignKey(e => e.CardTerminalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CashCountLineId, e.CardTerminalId, e.BatchNumber }).IsUnique()
            .HasDatabaseName("UK_INV_CashCountTerminalBatches_Line_Terminal_Batch").HasFilter("[IsDeleted] = 0");
    }
}
