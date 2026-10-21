using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationCollaterals</c> (feature 013, K1; data-model §29.3; plantillas.md §5.3.7): hecho de preparación; único <c>(tanda, fila)</c>;
/// aseguradora y fondo de garantías son personas; cobertura y comisión como fracción.
/// </summary>
public class MigrationCollateralRowConfiguration : IEntityTypeConfiguration<MigrationCollateralRow>
{
    public const string Tabla = "LND_MigrationCollaterals";

    public void Configure(EntityTypeBuilder<MigrationCollateralRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CollateralKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Value).Monto().IsRequired();
        builder.Property(e => e.ConstitutedOn).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Description).HasMaxLength(400);
        builder.Property(e => e.AppraisalValue).Monto();
        builder.Property(e => e.PolicyNumber).HasMaxLength(40);
        builder.Property(e => e.PromissoryNoteKind).HasConversion<int?>();
        builder.Property(e => e.CustodianChannelCode).HasMaxLength(30);
        builder.Property(e => e.ExternalNoteId).HasMaxLength(60);
        builder.Property(e => e.CoveragePercentage).Fraccion();
        builder.Property(e => e.FeePercentage).Fraccion();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.InsurerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.GuaranteeFundPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationCollaterals_Part_Row");
    }
}
