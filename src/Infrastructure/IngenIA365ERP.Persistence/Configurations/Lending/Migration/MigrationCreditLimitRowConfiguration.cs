using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationCreditLimits</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; el monto
/// del cupo en pesos.
/// </summary>
public class MigrationCreditLimitRowConfiguration : IEntityTypeConfiguration<MigrationCreditLimitRow>
{
    public const string Tabla = "LND_MigrationCreditLimits";

    public void Configure(EntityTypeBuilder<MigrationCreditLimitRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.LimitKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.CreditLineCode).HasMaxLength(10);
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationCreditLimits_Part_Row");
    }
}
