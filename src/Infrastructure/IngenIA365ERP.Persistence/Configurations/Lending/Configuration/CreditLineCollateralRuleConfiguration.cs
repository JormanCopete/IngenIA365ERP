using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_CreditLineCollateralRules</c> (feature 013, T0210; data-model §1.7): único <c>(Version, Order)</c>.</summary>
public class CreditLineCollateralRuleConfiguration : IEntityTypeConfiguration<CreditLineCollateralRule>
{
    public const string Tabla = "LND_CreditLineCollateralRules";

    public void Configure(EntityTypeBuilder<CreditLineCollateralRule> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.MinAmount).Monto();
        builder.Property(e => e.MaxAmount).Monto();
        builder.Property(e => e.RequiredCoDebtors).IsRequired();
        builder.Property(e => e.AdmittedKinds).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MinimumCoverage).Fraccion();
        builder.Property(e => e.RequiredPromissoryNoteKind).HasConversion<int?>();

        builder.HasOne<CreditLineVersion>().WithMany().HasForeignKey(e => e.CreditLineVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CreditLineVersionId, e.Order }).IsUnique()
            .HasDatabaseName("UK_LND_CreditLineCollateralRules_Version_Order");
    }
}
