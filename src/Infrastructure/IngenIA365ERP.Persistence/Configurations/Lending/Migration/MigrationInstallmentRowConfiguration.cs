using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationInstallments</c> (feature 013, K1; data-model §29.3): hecho de preparación con <c>bigint Id</c>; único
/// <c>(tanda, fila)</c>; montos pendientes al corte en pesos.
/// </summary>
public class MigrationInstallmentRowConfiguration : IEntityTypeConfiguration<MigrationInstallmentRow>
{
    public const string Tabla = "LND_MigrationInstallments";

    public void Configure(EntityTypeBuilder<MigrationInstallmentRow> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.InstallmentNumber).IsRequired();
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.Principal).Monto().IsRequired();
        builder.Property(e => e.Interest).Monto().IsRequired();
        builder.Property(e => e.Insurance).Monto().IsRequired();
        builder.Property(e => e.OtherCharges).Monto().IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationInstallments_Part_Row");
    }
}
