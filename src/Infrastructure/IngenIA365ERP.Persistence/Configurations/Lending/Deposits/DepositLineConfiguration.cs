using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

/// <summary>
/// <c>LND_DepositLineVersions</c> (feature 014, US1, T063; data-model §2.1): hecho de sólo inserción. Único <c>(CreditLineId, VersionNumber)</c> sin
/// filtro; índice de lectura bitemporal <c>(CreditLineId, ValidFrom, VersionNumber)</c>. Tasas como fracción (<c>TasaAnual</c>), montos en pesos y el
/// descuento de la redención como <c>Fraccion</c>. La línea destino de un CDAT que pasa a la vista es otra fila de <c>LND_CreditLines</c>.
/// Migración <c>CaptacionesAportes</c> (research E35).
/// </summary>
public class DepositLineVersionConfiguration : IEntityTypeConfiguration<DepositLineVersion>
{
    public const string Tabla = "LND_DepositLineVersions";

    public void Configure(EntityTypeBuilder<DepositLineVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();
        builder.Property(e => e.DepositClass).HasConversion<int>().IsRequired();

        builder.Property(e => e.BranchPublicIdsJson);

        builder.Property(e => e.RateKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.AnnualRate).TasaAnual();
        builder.Property(e => e.IndexCode).HasMaxLength(20);
        builder.Property(e => e.IndexKey).HasMaxLength(20);
        builder.Property(e => e.Margin).TasaAnual();
        builder.Property(e => e.RateFloor).TasaAnual();
        builder.Property(e => e.RateCeiling).TasaAnual();
        builder.Property(e => e.ResetPeriodicity).HasConversion<int?>();
        builder.Property(e => e.RateTierMode).HasConversion<int?>();

        builder.Property(e => e.BalanceMethod).HasConversion<int>().IsRequired();
        builder.Property(e => e.DayCount).HasConversion<int>().IsRequired();
        builder.Property(e => e.RateEquivalence).HasConversion<int>().IsRequired();
        builder.Property(e => e.RoundingUnit).Monto().IsRequired();
        builder.Property(e => e.RoundingMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.RoundingMoment).HasConversion<int>().IsRequired();
        builder.Property(e => e.MinOpeningAmount).Monto().IsRequired();
        builder.Property(e => e.MinBalance).Monto().IsRequired();
        builder.Property(e => e.MinBalanceForInterest).Monto().IsRequired();
        builder.Property(e => e.SettlementPeriodicity).HasConversion<int>().IsRequired();

        builder.Property(e => e.WithholdingRateParameterCode).HasMaxLength(60).IsRequired();

        builder.Property(e => e.MaxWithdrawalPerOperation).Monto();
        builder.Property(e => e.MaxWithdrawalPerDay).Monto();
        builder.Property(e => e.MaxCashPerOperation).Monto();
        builder.Property(e => e.MaxCashPerDay).Monto();

        builder.Property(e => e.InterestPeriodicities).HasMaxLength(200);
        builder.Property(e => e.InterestTimings).HasMaxLength(50);
        builder.Property(e => e.RolloverRateRule).HasConversion<int?>();
        builder.Property(e => e.AfterMaturityTreatment).HasConversion<int?>();
        builder.Property(e => e.EarlyRedemptionPenaltyMode).HasConversion<int?>();
        builder.Property(e => e.PenaltyAnnualRate).TasaAnual();
        builder.Property(e => e.PenaltyDiscountFraction).Fraccion();

        builder.Property(e => e.InstallmentPeriodicity).HasConversion<int?>();
        builder.Property(e => e.MinInstallmentAmount).Monto();
        builder.Property(e => e.BonusAnnualRate).TasaAnual();
        builder.Property(e => e.BreachAnnualRate).TasaAnual();
        builder.Property(e => e.BreachEffect).HasConversion<int?>();
        builder.Property(e => e.BonusRecognition).HasConversion<int?>();

        builder.Property(e => e.ContentSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();

        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.MoveToDemandLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CreditLineId, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_LND_DepositLineVersions_Line_Version");
        builder.HasIndex(e => new { e.CreditLineId, e.ValidFrom, e.VersionNumber })
            .HasDatabaseName("IX_LND_DepositLineVersions_Line_ValidFrom");
        builder.HasIndex(e => e.MoveToDemandLineId).HasDatabaseName("IX_LND_DepositLineVersions_MoveToDemandLineId");
    }
}

/// <summary><c>LND_DepositLineRateTiers</c> (feature 014, US1, T063; data-model §2.2): hija de la versión, hecho. Único <c>(DepositLineVersionId, Order)</c>.</summary>
public class DepositLineRateTierConfiguration : IEntityTypeConfiguration<DepositLineRateTier>
{
    public const string Tabla = "LND_DepositLineRateTiers";

    public void Configure(EntityTypeBuilder<DepositLineRateTier> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.MinBalance).Monto();
        builder.Property(e => e.MaxBalance).Monto();
        builder.Property(e => e.MinAmount).Monto();
        builder.Property(e => e.MaxAmount).Monto();
        builder.Property(e => e.AnnualRate).TasaAnual();
        builder.Property(e => e.Margin).TasaAnual();

        // Nombre propio: el generado pasa de 63 caracteres y PostgreSQL lo truncaba (research E18).
        builder.HasOne<DepositLineVersion>().WithMany().HasForeignKey(e => e.DepositLineVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DepositLineRateTiers_Version");

        builder.HasIndex(e => new { e.DepositLineVersionId, e.Order }).IsUnique()
            .HasDatabaseName("UK_LND_DepositLineRateTiers_Version_Order");
    }
}

/// <summary>
/// <c>LND_ContributionLineVersions</c> (feature 014, US1, T063; data-model §2.5): hecho de sólo inserción, sin tasa. Único
/// <c>(CreditLineId, VersionNumber)</c>; la línea del ahorro permanente que recibe su parte es otra fila de <c>LND_CreditLines</c>. Migración
/// <c>CaptacionesAportes</c>.
/// </summary>
public class ContributionLineVersionConfiguration : IEntityTypeConfiguration<ContributionLineVersion>
{
    public const string Tabla = "LND_ContributionLineVersions";

    public void Configure(EntityTypeBuilder<ContributionLineVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.Property(e => e.MinimumMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.MinimumAmount).Monto();
        builder.Property(e => e.MinimumFraction).Fraccion();
        builder.Property(e => e.InstallmentMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.InstallmentAmount).Monto();
        builder.Property(e => e.InstallmentFraction).Fraccion();
        builder.Property(e => e.InstallmentRoundingUnit).Monto().IsRequired();
        builder.Property(e => e.InstallmentRoundingMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.InstallmentPeriodicity).HasConversion<int>().IsRequired();
        builder.Property(e => e.PermanentSavingsFraction).Fraccion();
        builder.Property(e => e.MaxParticipationFraction).Fraccion();
        builder.Property(e => e.ExcessAction).HasConversion<int>().IsRequired();
        builder.Property(e => e.ContentSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();

        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        // Nombre propio: el generado pasa de 63 caracteres y PostgreSQL lo truncaba (research E18).
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.PermanentSavingsLineId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_ContributionLineVersions_PermanentSavingsLine");

        builder.HasIndex(e => new { e.CreditLineId, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_LND_ContributionLineVersions_Line_Version");
        builder.HasIndex(e => new { e.CreditLineId, e.ValidFrom, e.VersionNumber })
            .HasDatabaseName("IX_LND_ContributionLineVersions_Line_ValidFrom");
        builder.HasIndex(e => e.PermanentSavingsLineId).HasDatabaseName("IX_LND_ContributionLineVersions_PermanentSavingsLineId");
    }
}
