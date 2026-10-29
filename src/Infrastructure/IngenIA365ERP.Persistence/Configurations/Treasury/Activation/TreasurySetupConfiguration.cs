using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.Activation;

/// <summary>
/// <c>TES_Setup</c> (feature 015, T0036; data-model §9.1): fila única de la Tesorería nueva, nivel 1 de su cerrojo. La siembra
/// <c>TesoreriaNucleo</c> con <c>Id = 1</c>. Vive en <c>Configurations/Treasury/Activation</c>: la raíz es de lo heredado.
/// </summary>
public class TreasurySetupConfiguration : IEntityTypeConfiguration<TreasurySetup>
{
    public const string Tabla = "TES_Setup";

    public void Configure(EntityTypeBuilder<TreasurySetup> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.ActivatedOn);
        builder.Property(e => e.ResultWithThirdPerson).HasDefaultValue(false);
    }
}
