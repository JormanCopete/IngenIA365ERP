using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Periods;

/// <summary><c>INV_DeferredCosts</c> (2026-11-18): el costo que espera el cierre del mes. <c>IX (Year, Month, PeriodCostPostingId)</c> para el cierre.</summary>
public class DeferredCostConfiguration : IEntityTypeConfiguration<DeferredCost>
{
    public void Configure(EntityTypeBuilder<DeferredCost> builder)
    {
        builder.ComoEntidadDeInventario("INV_DeferredCosts");

        builder.Property(e => e.ContentType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ContentJson).IsRequired();

        builder.HasOne(e => e.SourceDocument).WithMany().HasForeignKey(e => e.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostDocument).WithMany().HasForeignKey(e => e.CostDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PeriodCostPosting).WithMany().HasForeignKey(e => e.PeriodCostPostingId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Year, e.Month, e.PeriodCostPostingId }).HasDatabaseName("IX_INV_DeferredCosts_Year_Month_Posting");
        builder.HasIndex(e => e.CostDocumentId).HasDatabaseName("IX_INV_DeferredCosts_CostDocument");
    }
}

/// <summary><c>INV_PeriodCostPostings</c> (2026-11-18): un envío del costo del mes, o su reversión.</summary>
public class PeriodCostPostingConfiguration : IEntityTypeConfiguration<PeriodCostPosting>
{
    public void Configure(EntityTypeBuilder<PeriodCostPosting> builder)
    {
        builder.ComoEntidadDeInventario("INV_PeriodCostPostings");

        builder.HasOne(e => e.Period).WithMany().HasForeignKey(e => e.PeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.PeriodId, e.CloseVersion, e.IsReversal }).IsUnique()
            .HasDatabaseName("UK_INV_PeriodCostPostings_Period_Version_Reversal");
    }
}
