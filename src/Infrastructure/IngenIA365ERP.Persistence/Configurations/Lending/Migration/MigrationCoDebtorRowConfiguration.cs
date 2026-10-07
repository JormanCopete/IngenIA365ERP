using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationCoDebtors</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; el codeudor
/// es una persona y su rol va como entero.
/// </summary>
public class MigrationCoDebtorRowConfiguration : IEntityTypeConfiguration<MigrationCoDebtorRow>
{
    public const string Tabla = "LND_MigrationCoDebtors";

    public void Configure(EntityTypeBuilder<MigrationCoDebtorRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Role).HasConversion<int>().IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.CoDebtorPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationCoDebtors_Part_Row");
    }
}
