using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationRiskData</c> (feature 013, K1; data-model §29.3; plantillas.md §5.3.4): hecho de preparación, una fila por crédito; único
/// <c>(tanda, fila)</c>; deterioros en pesos; categoría, subcategoría y causa como enteros.
/// </summary>
public class MigrationRiskDataRowConfiguration : IEntityTypeConfiguration<MigrationRiskDataRow>
{
    public const string Tabla = "LND_MigrationRiskData";

    public void Configure(EntityTypeBuilder<MigrationRiskDataRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.DaysPastDue).IsRequired();
        builder.Property(e => e.Rating).HasConversion<int>().IsRequired();
        builder.Property(e => e.Subcategory).HasConversion<int>().IsRequired();
        builder.Property(e => e.Cause).HasConversion<int?>();
        builder.Property(e => e.PrincipalImpairment).Monto().IsRequired();
        builder.Property(e => e.InterestImpairment).Monto().IsRequired();
        builder.Property(e => e.OtherImpairment).Monto().IsRequired();
        builder.Property(e => e.GeneralImpairment).Monto().IsRequired();
        builder.Property(e => e.IsSuspended).IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationRiskData_Part_Row");
    }
}
