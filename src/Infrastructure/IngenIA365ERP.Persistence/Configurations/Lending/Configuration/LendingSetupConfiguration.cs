using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary>
/// <c>LND_Setup</c> (feature 013, T0210; data-model §1.1): fila única de Cartera, primera del cerrojo (T34). La siembra la migración
/// <c>CarteraNucleo</c> con <c>Id = 1</c> (<c>INSERT … WHERE NOT EXISTS</c>). <c>ActivationId</c> apunta a <c>LND_Activations</c>,
/// que no apunta de vuelta: no hay ciclo (C66).
/// </summary>
public class LendingSetupConfiguration : IEntityTypeConfiguration<LendingSetup>
{
    public const string Tabla = "LND_Setup";

    public void Configure(EntityTypeBuilder<LendingSetup> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ActivationDate);
        builder.Property(e => e.ActivatedAt);
        builder.HasOne<LendingActivation>().WithMany().HasForeignKey(e => e.ActivationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.ActivationId).HasDatabaseName("IX_LND_Setup_ActivationId");

        // Feature 014 (T053; data-model §1.2): la activación de captaciones y la de aportes, cada una a su fila de LND_Activations.
        builder.Property(e => e.DepositsActivationDate);
        builder.Property(e => e.DepositsActivatedAt);
        builder.HasOne<LendingActivation>().WithMany().HasForeignKey(e => e.DepositsActivationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.DepositsActivationId).HasDatabaseName("IX_LND_Setup_DepositsActivationId");
        builder.Property(e => e.ContributionsActivationDate);
        builder.Property(e => e.ContributionsActivatedAt);
        builder.HasOne<LendingActivation>().WithMany().HasForeignKey(e => e.ContributionsActivationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.ContributionsActivationId).HasDatabaseName("IX_LND_Setup_ContributionsActivationId");
    }
}
