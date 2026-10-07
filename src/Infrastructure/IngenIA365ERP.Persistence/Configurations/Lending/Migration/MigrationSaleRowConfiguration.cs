using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationSales</c> (feature 013, K1; data-model §29.3; FR-143): hecho de preparación; único <c>(tanda, fila)</c>; montos en
/// pesos y CHECK de saldo al corte en cero (la parte sólo admite ventas canceladas antes del corte).
/// </summary>
public class MigrationSaleRowConfiguration : IEntityTypeConfiguration<MigrationSaleRow>
{
    public const string Tabla = "LND_MigrationSales";

    public void Configure(EntityTypeBuilder<MigrationSaleRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_MigrationSales_BalanceAtCutoff", "[BalanceAtCutoff] = 0"));

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.SaleDate).IsRequired();
        builder.Property(e => e.OriginalAmount).Monto().IsRequired();
        builder.Property(e => e.ExternalCollections).Monto().IsRequired();
        builder.Property(e => e.AdjustmentsAmount).Monto().IsRequired();
        builder.Property(e => e.BalanceAtCutoff).Monto().IsRequired();
        builder.Property(e => e.CreditLineCode).HasMaxLength(10);
        builder.Property(e => e.CancellationReason).HasMaxLength(500).IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationSales_Part_Row");
    }
}
