using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationAuthorizations</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; finalidad
/// y canal son enums de Compliance guardados como texto.
/// </summary>
public class MigrationAuthorizationRowConfiguration : IEntityTypeConfiguration<MigrationAuthorizationRow>
{
    public const string Tabla = "LND_MigrationAuthorizations";

    public void Configure(EntityTypeBuilder<MigrationAuthorizationRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ContactChannel).HasMaxLength(30);
        builder.Property(e => e.AuthorizedOn).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PolicyVersionText).HasMaxLength(30);
        builder.Property(e => e.EvidenceReference).HasMaxLength(200);
        builder.Property(e => e.HasEvidence).IsRequired();
        builder.Property(e => e.ObtainedThrough).HasMaxLength(60);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationAuthorizations_Part_Row");
    }
}
