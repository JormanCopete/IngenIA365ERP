using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationClaims</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; clase de
/// reclamo como entero.
/// </summary>
public class MigrationClaimRowConfiguration : IEntityTypeConfiguration<MigrationClaimRow>
{
    public const string Tabla = "LND_MigrationClaims";

    public void Configure(EntityTypeBuilder<MigrationClaimRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.ReceivedOn).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.BureauCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.IsExtended).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(400);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationClaims_Part_Row");
    }
}
