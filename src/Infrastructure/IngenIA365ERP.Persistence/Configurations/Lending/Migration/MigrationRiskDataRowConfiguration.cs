using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationRiskData</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; deterioros en
/// pesos y porcentaje como fracción; las columnas de cada clase de registro (<c>LOAN</c>/<c>GENERAL</c>) son nulas.
/// </summary>
public class MigrationRiskDataRowConfiguration : IEntityTypeConfiguration<MigrationRiskDataRow>
{
    public const string Tabla = "LND_MigrationRiskData";

    public void Configure(EntityTypeBuilder<MigrationRiskDataRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.RecordKind).HasMaxLength(10).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30);
        builder.Property(e => e.Rating).HasConversion<int?>();
        builder.Property(e => e.Subcategory).HasConversion<int?>();
        builder.Property(e => e.PrincipalImpairment).Monto();
        builder.Property(e => e.InterestImpairment).Monto();
        builder.Property(e => e.OtherImpairment).Monto();
        builder.Property(e => e.ImpairmentPercentage).Fraccion();
        builder.Property(e => e.ModalityCode).HasMaxLength(40);
        builder.Property(e => e.SubsegmentCode).HasMaxLength(30);
        builder.Property(e => e.GeneralImpairment).Monto();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationRiskData_Part_Row");
    }
}
