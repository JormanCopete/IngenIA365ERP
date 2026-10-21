using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationRestructurings</c> (feature 013, K1; data-model §29.3, C97): hecho de preparación; único <c>(tanda, fila)</c>;
/// causa y calificación como entero.
/// </summary>
public class MigrationRestructuringRowConfiguration : IEntityTypeConfiguration<MigrationRestructuringRow>
{
    public const string Tabla = "LND_MigrationRestructurings";

    public void Configure(EntityTypeBuilder<MigrationRestructuringRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Cause).HasConversion<int>().IsRequired();
        builder.Property(e => e.RestructuringCount).IsRequired();
        builder.Property(e => e.LastRestructuredOn).IsRequired();
        builder.Property(e => e.RatingAtRestructuring).HasConversion<int>().IsRequired();
        builder.Property(e => e.ConsecutivePaidInstallments).IsRequired();
        builder.Property(e => e.PredecessorLoanNumber).HasMaxLength(30);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationRestructurings_Part_Row");
    }
}
