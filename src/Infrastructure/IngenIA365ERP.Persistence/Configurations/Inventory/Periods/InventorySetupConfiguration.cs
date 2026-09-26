using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Periods;

/// <summary>
/// <c>INV_Setup</c> (feature 012, T284; data-model §6.1): fila única de la puesta en marcha. La crea el primer registro de una
/// fecha de corte (US4); el cerrojo (<c>SqlDelCerrojo.TablaSetup</c>) la bloquea compartida al confirmar y exclusiva al cerrar
/// o reabrir un período.
/// </summary>
public class InventorySetupConfiguration : IEntityTypeConfiguration<InventorySetup>
{
    public void Configure(EntityTypeBuilder<InventorySetup> builder)
    {
        builder.ComoEntidadDeInventario("INV_Setup");

        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.LastClosedDate);
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.StartedByUserId).IsRequired();
    }
}
