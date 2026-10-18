using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_AttributionRules</c> (feature 013, T0213; data-model §3.3): hecho hijo de la matriz. Único <c>(MatrixId, Order)</c> sin
/// filtro; montos en pesos; FK al nivel mínimo y a la línea.
/// </summary>
public class AttributionRuleConfiguration : IEntityTypeConfiguration<AttributionRule>
{
    public const string Tabla = "LND_AttributionRules";

    public void Configure(EntityTypeBuilder<AttributionRule> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ModalityCode).HasMaxLength(40);
        builder.Property(e => e.MinConsolidatedAmount).Monto();
        builder.Property(e => e.MaxConsolidatedAmount).Monto();
        builder.Property(e => e.CollateralKind).HasConversion<int?>();
        builder.Property(e => e.Concept).HasConversion<int?>();
        builder.Property(e => e.NoveltyMechanism).HasConversion<int?>();
        builder.Property(e => e.NoveltyCause).HasConversion<int?>();

        builder.HasOne<AttributionMatrix>().WithMany().HasForeignKey(e => e.MatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttributionLevel>().WithMany().HasForeignKey(e => e.MinimumLevelId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MatrixId, e.Order }).IsUnique().HasDatabaseName("UK_LND_AttributionRules_Matrix_Order");
    }
}
