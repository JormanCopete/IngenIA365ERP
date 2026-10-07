using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationBureauReports</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>.
/// </summary>
public class MigrationBureauReportRowConfiguration : IEntityTypeConfiguration<MigrationBureauReportRow>
{
    public const string Tabla = "LND_MigrationBureauReports";

    public void Configure(EntityTypeBuilder<MigrationBureauReportRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.BureauCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastReportedStatus).HasMaxLength(40).IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationBureauReports_Part_Row");
    }
}
