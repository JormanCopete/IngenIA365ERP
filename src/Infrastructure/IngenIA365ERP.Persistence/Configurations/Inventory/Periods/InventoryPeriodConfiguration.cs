using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Periods;

/// <summary>
/// <c>INV_Periods</c> (feature 012, T284; data-model §6.2): un mes de la cooperativa, con fila desde su primer cierre.
/// <c>UK_INV_Periods_Year_Month</c> filtrado a las vivas; el estado como entero; motivos de hasta 500.
/// </summary>
public class InventoryPeriodConfiguration : IEntityTypeConfiguration<InventoryPeriod>
{
    public void Configure(EntityTypeBuilder<InventoryPeriod> builder)
    {
        builder.ComoEntidadDeInventario("INV_Periods");

        builder.Property(e => e.Year).IsRequired();
        builder.Property(e => e.Month).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.CloseVersion).IsRequired();
        builder.Property(e => e.CloseWarningsJson);
        builder.Property(e => e.UnbilledShipmentsJson);
        builder.Property(e => e.UnbilledShipmentsAcceptedReason).HasMaxLength(500);
        builder.Property(e => e.ReopenReason).HasMaxLength(500);

        builder.Ignore(e => e.Inicio);
        builder.Ignore(e => e.Fin);

        builder.HasIndex(e => new { e.Year, e.Month }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_INV_Periods_Year_Month");
    }
}
