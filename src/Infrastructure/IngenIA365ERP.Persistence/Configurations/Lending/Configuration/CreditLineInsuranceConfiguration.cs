using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_CreditLineInsurances</c> (feature 013, T0210; data-model §1.6): único <c>(Version, Code)</c>; aseguradora = persona.</summary>
public class CreditLineInsuranceConfiguration : IEntityTypeConfiguration<CreditLineInsurance>
{
    public const string Tabla = "LND_CreditLineInsurances";

    public void Configure(EntityTypeBuilder<CreditLineInsurance> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Base).HasConversion<int>().IsRequired();
        builder.Property(e => e.Rate).TasaAnual().IsRequired();
        builder.Property(e => e.ChargePeriodicity).HasConversion<int>().IsRequired();
        builder.Property(e => e.AccrualTiming).HasConversion<int>().IsRequired();
        builder.Property(e => e.RetainedFraction).Fraccion();
        // 2026-11-16 (pedido del dueño; feature 024, fase 2; migración CausacionDeAdministracion): seguro o administración; las filas de antes, seguro.
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired().HasDefaultValue(InsuranceKind.Insurance);

        builder.HasOne<CreditLineVersion>().WithMany().HasForeignKey(e => e.CreditLineVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.InsurerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CreditLineVersionId, e.Code }).IsUnique()
            .HasDatabaseName("UK_LND_CreditLineInsurances_Version_Code");
    }
}
