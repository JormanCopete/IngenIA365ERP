using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationCollaterals</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; el
/// propietario es una persona.
/// </summary>
public class MigrationCollateralRowConfiguration : IEntityTypeConfiguration<MigrationCollateralRow>
{
    public const string Tabla = "LND_MigrationCollaterals";

    public void Configure(EntityTypeBuilder<MigrationCollateralRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.CollateralKey).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CollateralKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.IsMortgage).IsRequired();
        builder.Property(e => e.CommercialValue).Monto().IsRequired();
        builder.Property(e => e.PolicyNumber).HasMaxLength(40);
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.OwnerPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationCollaterals_Part_Row");
    }
}
