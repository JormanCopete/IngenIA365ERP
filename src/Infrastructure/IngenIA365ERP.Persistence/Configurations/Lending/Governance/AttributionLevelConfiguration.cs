using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_AttributionLevels</c> (feature 013, T0213; data-model §3.2): hecho hijo de la matriz. Únicos <c>(MatrixId, Code)</c> y
/// <c>(MatrixId, Rank)</c> sin filtro; techo de monto en pesos.
/// </summary>
public class AttributionLevelConfiguration : IEntityTypeConfiguration<AttributionLevel>
{
    public const string Tabla = "LND_AttributionLevels";

    public void Configure(EntityTypeBuilder<AttributionLevel> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Rank).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.PermissionCode).HasMaxLength(100);
        builder.Property(e => e.MaxConsolidatedAmount).Monto();
        builder.Property(e => e.IsExceptionLevel).IsRequired();
        builder.Property(e => e.IsBoardLevel).IsRequired();

        builder.HasOne<AttributionMatrix>().WithMany().HasForeignKey(e => e.MatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoverningBody>().WithMany().HasForeignKey(e => e.GoverningBodyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MatrixId, e.Code }).IsUnique().HasDatabaseName("UK_LND_AttributionLevels_Matrix_Code");
        builder.HasIndex(e => new { e.MatrixId, e.Rank }).IsUnique().HasDatabaseName("UK_LND_AttributionLevels_Matrix_Rank");
    }
}
