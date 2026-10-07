using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_Activations</c> (feature 013, K1; data-model §30.1): hecho, una por carga (<c>UK (MigrationLoadId)</c>, sin filtro); el
/// acta (<c>SessionItemId</c>) es obligatoria (C61); el adjunto de la revisión de cobros provisionales va sin FK (C62).
/// </summary>
public class LendingActivationConfiguration : IEntityTypeConfiguration<LendingActivation>
{
    public const string Tabla = "LND_Activations";

    public void Configure(EntityTypeBuilder<LendingActivation> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.CutoffDate).IsRequired();
        builder.Property(e => e.ActivationDate).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.CheckVersion).IsRequired();
        builder.Property(e => e.BalanceCheckSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.SalesReconciliationSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.IsProductionEnvironment).IsRequired();
        builder.Property(e => e.RequirementsJson).IsRequired();
        builder.Property(e => e.ObligatoryModelUnavailable).IsRequired();
        builder.Property(e => e.CountsJson).IsRequired();
        builder.Property(e => e.NonDeductionsMarked).IsRequired();
        builder.Property(e => e.CreditLimitsToRevalidate).IsRequired();
        builder.Property(e => e.ProvisionalCollectionsFileSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.ProvisionalCollectionsCount).IsRequired();
        builder.Property(e => e.CommercialAdjustmentsJson).IsRequired();
        builder.Property(e => e.PayrollWarningAcknowledged).IsRequired();
        builder.Property(e => e.ActivatedMessagePublicId).IsRequired();

        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.SessionItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MigrationLoadId).IsUnique().HasDatabaseName("UK_LND_Activations_MigrationLoadId");
    }
}
