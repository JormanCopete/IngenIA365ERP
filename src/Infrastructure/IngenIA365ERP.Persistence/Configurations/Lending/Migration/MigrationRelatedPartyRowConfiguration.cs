using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationRelatedParties</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; la
/// persona y aquella con la que se vincula, con FK a <c>COR_People</c>.
/// </summary>
public class MigrationRelatedPartyRowConfiguration : IEntityTypeConfiguration<MigrationRelatedPartyRow>
{
    public const string Tabla = "LND_MigrationRelatedParties";

    public void Configure(EntityTypeBuilder<MigrationRelatedPartyRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.Basis).HasConversion<int>().IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.SupportReference).HasMaxLength(200);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.RelatedToPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationRelatedParties_Part_Row");
    }
}
