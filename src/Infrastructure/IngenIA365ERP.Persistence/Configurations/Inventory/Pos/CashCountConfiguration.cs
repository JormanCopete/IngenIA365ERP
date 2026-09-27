using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashCounts</c> (feature 012, I3, T583; FR-099; data-model §15): un arqueo por sesión (único <c>CashSessionId</c> entre
/// vivos); totales en pesos; <c>Status</c> (copia del estado del documento de diferencia) lo mira la guarda de inmutabilidad.
/// </summary>
public class CashCountConfiguration : IEntityTypeConfiguration<CashCount>
{
    public void Configure(EntityTypeBuilder<CashCount> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashCounts");

        builder.Property(e => e.TotalExpected).Monto().IsRequired();
        builder.Property(e => e.TotalCounted).Monto().IsRequired();
        builder.Property(e => e.TotalDifference).Monto().IsRequired();
        builder.Property(e => e.Status).HasDefaultValue(DocumentStatus.Draft).IsRequired();

        builder.HasOne(e => e.CashSession).WithMany().HasForeignKey(e => e.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CountedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DifferenceDocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CashSessionId).IsUnique().HasDatabaseName("UK_INV_CashCounts_CashSessionId").HasFilter("[IsDeleted] = 0");
    }
}
