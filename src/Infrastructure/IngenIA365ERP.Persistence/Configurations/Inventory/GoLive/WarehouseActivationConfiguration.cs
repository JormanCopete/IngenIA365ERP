using IngenIA365ERP.Domain.Entities.Inventory.GoLive;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.GoLive;

/// <summary>
/// <c>INV_WarehouseActivations</c> (feature 012, T307; data-model §6.4): una fila por bodega activada (<c>UK (WarehouseId)</c>
/// filtrado a las vivas), la comparación en JSON y la diferencia en <c>Monto</c>. Sin migración propia: entra al par
/// <c>InventarioComercialNucleo</c> del cierre de I1 (T440).
/// </summary>
public class WarehouseActivationConfiguration : IEntityTypeConfiguration<WarehouseActivation>
{
    public void Configure(EntityTypeBuilder<WarehouseActivation> builder)
    {
        builder.ComoEntidadDeInventario("INV_WarehouseActivations");

        builder.Property(e => e.CutoffDate).IsRequired();
        builder.Property(e => e.ComparisonJson).IsRequired();
        builder.Property(e => e.TotalDifference).Monto().IsRequired();
        builder.Property(e => e.IsBalanced).IsRequired();
        builder.Property(e => e.AcceptanceReason).HasMaxLength(WarehouseActivation.LargoDelMotivo);
        builder.Property(e => e.ActivatedAt).IsRequired();
        builder.Property(e => e.ActivatedByUserId).IsRequired();

        builder.HasOne<Warehouse>().WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.WarehouseId).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_INV_WarehouseActivations_Warehouse");
    }
}
