using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationPriorNotices</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; el canal
/// como texto.
/// </summary>
public class MigrationPriorNoticeRowConfiguration : IEntityTypeConfiguration<MigrationPriorNoticeRow>
{
    public const string Tabla = "LND_MigrationPriorNotices";

    public void Configure(EntityTypeBuilder<MigrationPriorNoticeRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.EpisodeStartDate).IsRequired();
        builder.Property(e => e.RequiredCommunications).IsRequired();
        builder.Property(e => e.FirstSentOn).IsRequired();
        builder.Property(e => e.FirstChannel).HasConversion<int>().IsRequired();
        builder.Property(e => e.FirstReference).HasMaxLength(100);
        builder.Property(e => e.SecondChannel).HasConversion<int?>();
        builder.Property(e => e.SecondReference).HasMaxLength(100);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationPriorNotices_Part_Row");
    }
}
