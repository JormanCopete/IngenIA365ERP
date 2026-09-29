using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Transactions;

/// <summary>
/// <c>INV_LayerConsumptions</c> (feature 012, I5, T833; data-model §3.5): el hecho de consumir (o, con cantidad negativa, devolver)
/// una capa PEPS, <c>bigint</c> y sólo inserción. <c>IX (ExitKardexEntryId)</c> e <c>IX (LayerId)</c>; FK <c>Restrict</c> a la línea
/// del kardex y a la capa. Cantidad (18,4), costo (18,6). Adelantada por la sección de dominio de costeo (la exige
/// <c>LasCantidadesYCostosTienenSuPrecision</c>); el DbSet y la migración <c>ComprasYCosteoAvanzado</c> son de la sección de
/// persistencia (T835).
/// </summary>
public class LayerConsumptionConfiguration : IEntityTypeConfiguration<LayerConsumption>
{
    public const string Tabla = "INV_LayerConsumptions";

    public void Configure(EntityTypeBuilder<LayerConsumption> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");

        builder.Property(e => e.Quantity).Cantidad().IsRequired();
        builder.Property(e => e.UnitCost).CostoUnitario().IsRequired();

        builder.HasOne(e => e.ExitKardexEntry).WithMany().HasForeignKey(e => e.ExitKardexEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Layer).WithMany().HasForeignKey(e => e.LayerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ExitKardexEntryId).HasDatabaseName("IX_INV_LayerConsumptions_ExitKardexEntryId");
        builder.HasIndex(e => e.LayerId).HasDatabaseName("IX_INV_LayerConsumptions_LayerId");

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
