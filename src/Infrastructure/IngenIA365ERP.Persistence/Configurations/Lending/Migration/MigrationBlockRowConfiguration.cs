using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationBlocks</c> (feature 013, K1; data-model §29.3, C64): hecho de preparación; único <c>(tanda, fila)</c>; causa y
/// estados de reclamación e insolvencia como entero; la aseguradora es una persona.
/// </summary>
public class MigrationBlockRowConfiguration : IEntityTypeConfiguration<MigrationBlockRow>
{
    public const string Tabla = "LND_MigrationBlocks";

    public void Configure(EntityTypeBuilder<MigrationBlockRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.BlockReason).HasConversion<int>().IsRequired();
        builder.Property(e => e.Since).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30);
        builder.Property(e => e.SupportReference).HasMaxLength(200).IsRequired();
        builder.Property(e => e.InsuranceClaimStatus).HasConversion<int?>();
        builder.Property(e => e.InsolvencyStatus).HasConversion<int?>();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.InsurerPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationBlocks_Part_Row");
    }
}
