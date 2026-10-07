using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationLoadParts</c> (feature 013, K1; data-model §29.2, C64): único <c>(carga, parte, tanda)</c> y el mismo archivo una
/// sola vez entre las tandas <c>Imported</c> de la parte (filtro <c>[Status] = 1</c>).
/// </summary>
public class MigrationLoadPartConfiguration : IEntityTypeConfiguration<MigrationLoadPart>
{
    public const string Tabla = "LND_MigrationLoadParts";

    public void Configure(EntityTypeBuilder<MigrationLoadPart> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.PartKey).HasMaxLength(40).IsRequired();
        builder.Property(e => e.BatchNumber).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(260).IsRequired();
        builder.Property(e => e.FileSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.RowCount).IsRequired();
        builder.Property(e => e.ImportedAt).IsRequired();
        builder.Property(e => e.ReviewSummaryJson);

        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ImportedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadId, e.PartKey, e.BatchNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationLoadParts_Load_Part_Batch");
        builder.HasIndex(e => new { e.MigrationLoadId, e.PartKey, e.FileSha256 }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationLoadParts_Load_Part_File")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");
    }
}
