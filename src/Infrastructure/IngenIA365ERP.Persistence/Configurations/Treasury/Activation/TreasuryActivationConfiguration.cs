using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.Activation;

/// <summary>
/// <c>TES_Activations</c> (feature 015, T0183b; data-model §15.1): las activaciones de Tesorería. La FK de <c>OpeningSquareId</c> llega con el par
/// <c>RecaudoYTesoreriaPuestaEnMarcha</c>. (nuevo)
/// </summary>
public class TreasuryActivationConfiguration : IEntityTypeConfiguration<TreasuryActivation>
{
    public const string Tabla = "TES_Activations";

    public void Configure(EntityTypeBuilder<TreasuryActivation> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActivatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.RequirementsJson).HasMaxLength(8000).IsRequired();
        builder.HasIndex(e => e.ActivatedOn).HasDatabaseName($"IX_{Tabla}_ActivatedOn");
    }
}
