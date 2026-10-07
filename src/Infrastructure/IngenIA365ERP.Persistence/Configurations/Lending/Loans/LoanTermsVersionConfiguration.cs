using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Loans;

/// <summary>
/// <c>LND_LoanTermsVersions</c> (feature 013; data-model §4.3): hecho; único <c>(LoanId, VersionNumber)</c> sin filtro. Tasas pactadas
/// como fracción (<c>TasaAnual</c>), efectiva resultante y sobretasa de cargos con <c>TasaEquivalente</c>, redondeo en pesos.
/// </summary>
public class LoanTermsVersionConfiguration : IEntityTypeConfiguration<LoanTermsVersion>
{
    public const string Tabla = "LND_LoanTermsVersions";

    public void Configure(EntityTypeBuilder<LoanTermsVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.EffectiveValueDate).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Cause).HasConversion<int>().IsRequired();
        builder.Property(e => e.RulesVersion).HasMaxLength(20).IsRequired();

        builder.Property(e => e.ModalityCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SubsegmentCode).HasMaxLength(30);
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

        builder.Property(e => e.DayCount).HasConversion<int>().IsRequired();
        builder.Property(e => e.RateEquivalence).HasConversion<int>().IsRequired();
        builder.Property(e => e.AmortizationSystem).HasConversion<int>().IsRequired();
        builder.Property(e => e.Periodicity).HasConversion<int>().IsRequired();
        builder.Property(e => e.FirstDueDate).IsRequired();
        builder.Property(e => e.GraceKind).HasConversion<int?>();
        builder.Property(e => e.GraceInterestTreatment).HasConversion<int?>();
        builder.Property(e => e.ExtraordinaryInstallments);
        builder.Property(e => e.RoundingUnit).Monto().IsRequired();
        builder.Property(e => e.RoundingMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.RoundingMoment).HasConversion<int>().IsRequired();

        builder.Property(e => e.AllocationOrder).HasConversion<int>().IsRequired();
        builder.Property(e => e.ConceptPriority).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NoteRecalculation).HasConversion<int>().IsRequired();

        builder.Property(e => e.Insurances).IsRequired();
        builder.Property(e => e.ResultingEffectiveRate).TasaEquivalente();
        builder.Property(e => e.ChargesSpread).TasaEquivalente();
        builder.Property(e => e.InputsSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();

        builder.HasOne(e => e.Loan).WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLineVersion>().WithMany().HasForeignKey(e => e.CreditLineVersionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_LND_LoanTermsVersions_Loan_Version");
    }
}
