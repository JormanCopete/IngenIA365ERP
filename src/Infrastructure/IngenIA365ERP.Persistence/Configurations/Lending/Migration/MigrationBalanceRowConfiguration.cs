using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationBalances</c> (feature 013, K1; data-model §29.3): hecho de preparación con <c>bigint Id</c>; único
/// <c>(tanda, fila)</c>; concepto como entero.
/// </summary>
public class MigrationBalanceRowConfiguration : IEntityTypeConfiguration<MigrationBalanceRow>
{
    public const string Tabla = "LND_MigrationBalances";

    public void Configure(EntityTypeBuilder<MigrationBalanceRow> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Concept).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationBalances_Part_Row");
    }
}
