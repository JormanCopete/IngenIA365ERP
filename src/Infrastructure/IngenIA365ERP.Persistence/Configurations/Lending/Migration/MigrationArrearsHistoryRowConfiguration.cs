using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationArrearsHistory</c> (feature 013, K1; data-model §29.3): hecho de preparación con <c>bigint Id</c>; único
/// <c>(tanda, fila)</c>; período como <c>smallint</c>/<c>tinyint</c>.
/// </summary>
public class MigrationArrearsHistoryRowConfiguration : IEntityTypeConfiguration<MigrationArrearsHistoryRow>
{
    public const string Tabla = "LND_MigrationArrearsHistory";

    public void Configure(EntityTypeBuilder<MigrationArrearsHistoryRow> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PeriodYear).IsRequired();
        builder.Property(e => e.PeriodMonth).IsRequired();
        builder.Property(e => e.MaxDaysPastDue).IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationArrearsHistory_Part_Row");
    }
}
