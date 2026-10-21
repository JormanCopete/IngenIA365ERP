using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationChargedOff</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; saldos del
/// castigado en pesos.
/// </summary>
public class MigrationChargedOffRowConfiguration : IEntityTypeConfiguration<MigrationChargedOffRow>
{
    public const string Tabla = "LND_MigrationChargedOff";

    public void Configure(EntityTypeBuilder<MigrationChargedOffRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CreditLineCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.WrittenOffOn).IsRequired();
        builder.Property(e => e.PrincipalBalance).Monto().IsRequired();
        builder.Property(e => e.InterestBalance).Monto().IsRequired();
        builder.Property(e => e.OtherBalance).Monto().IsRequired();
        builder.Property(e => e.OfficePublicId).IsRequired();
        builder.Property(e => e.HistoricalApprovalReference).HasMaxLength(200).IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationChargedOff_Part_Row");
    }
}
