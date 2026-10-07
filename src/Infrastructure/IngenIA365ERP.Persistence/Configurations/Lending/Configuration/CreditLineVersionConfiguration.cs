using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLineVersions</c> (feature 013, T0210; data-model §1.3): hecho (C42). Único <c>(CreditLineId, VersionNumber)</c> sin
/// filtro; índice de lectura bitemporal <c>(CreditLineId, ValidFrom, VersionNumber)</c>. Tasas como fracción (<c>TasaAnual</c>),
/// montos en pesos, fracciones y puntaje con su alias.
/// </summary>
public class CreditLineVersionConfiguration : IEntityTypeConfiguration<CreditLineVersion>
{
    public const string Tabla = "LND_CreditLineVersions";

    public void Configure(EntityTypeBuilder<CreditLineVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.Property(e => e.ModalityCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SubsegmentCode).HasMaxLength(30);
        builder.Property(e => e.CommercialDefaultFor).HasConversion<int?>();
        builder.Property(e => e.InterestTaxTreatment).HasConversion<int?>();

        builder.Property(e => e.RateKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.AnnualRate).TasaAnual();
        builder.Property(e => e.IndexCode).HasMaxLength(20);
        builder.Property(e => e.IndexKey).HasMaxLength(20);
        builder.Property(e => e.Margin).TasaAnual();
        builder.Property(e => e.RateFloor).TasaAnual();
        builder.Property(e => e.RateCeiling).TasaAnual();
        builder.Property(e => e.ResetPeriodicity).HasConversion<int?>();

        builder.Property(e => e.DefaultRateKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.DefaultRate).TasaAnual();
        builder.Property(e => e.DefaultRateFactor).TasaAnual();

        builder.Property(e => e.MinAmount).Monto().IsRequired();
        builder.Property(e => e.MaxAmount).Monto().IsRequired();
        builder.Property(e => e.Periodicities).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DefaultPeriodicity).HasConversion<int>().IsRequired();

        builder.Property(e => e.AmortizationSystems).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DefaultAmortizationSystem).HasConversion<int>().IsRequired();
        builder.Property(e => e.MaxExtraordinaryFraction).Fraccion();
        builder.Property(e => e.AllowedGraceKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.GraceInterestTreatment).HasConversion<int?>();
        builder.Property(e => e.DayCount).HasConversion<int>().IsRequired();
        builder.Property(e => e.RateEquivalence).HasConversion<int>().IsRequired();
        builder.Property(e => e.RoundingUnit).Monto().IsRequired();
        builder.Property(e => e.RoundingMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.RoundingMoment).HasConversion<int>().IsRequired();

        builder.Property(e => e.AllocationOrder).HasConversion<int>().IsRequired();
        builder.Property(e => e.ConceptPriority).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PrepaymentOfferedFirst).HasConversion<int>().IsRequired();
        builder.Property(e => e.NoteRecalculation).HasConversion<int>().IsRequired();

        builder.Property(e => e.RepaymentChannels).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DefaultRepaymentChannel).HasConversion<int>().IsRequired();

        builder.Property(e => e.ContributionsMultiple).Fraccion();
        builder.Property(e => e.MaxIndebtedness).Monto();
        builder.Property(e => e.MinContributionsBalance).Monto();

        builder.Property(e => e.ScoringModel);
        builder.Property(e => e.MinimumScore).Puntaje();
        builder.Property(e => e.ContentSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();

        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CreditLineId, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_LND_CreditLineVersions_Line_Version");
        builder.HasIndex(e => new { e.CreditLineId, e.ValidFrom, e.VersionNumber })
            .HasDatabaseName("IX_LND_CreditLineVersions_Line_ValidFrom");
    }
}
