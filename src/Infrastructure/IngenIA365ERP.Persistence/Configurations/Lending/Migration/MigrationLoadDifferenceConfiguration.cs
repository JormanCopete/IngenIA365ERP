using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationLoadDifferences</c> (feature 013, K1; data-model §29.4): hecho; único <c>(carga, versión, dimensión)</c> sin
/// filtro; montos en pesos; la aceptación (usuario y punto de acta) con FK <c>Restrict</c>.
/// </summary>
public class MigrationLoadDifferenceConfiguration : IEntityTypeConfiguration<MigrationLoadDifference>
{
    public const string Tabla = "LND_MigrationLoadDifferences";

    public void Configure(EntityTypeBuilder<MigrationLoadDifference> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.CheckVersion).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.DimensionKey).HasMaxLength(300).IsRequired();
        builder.Property(e => e.ConceptFamily).HasMaxLength(40);
        builder.Property(e => e.ModalityCode).HasMaxLength(40);
        builder.Property(e => e.SubsegmentCode).HasMaxLength(40);
        builder.Property(e => e.RiskCategory).HasConversion<int?>();
        builder.Property(e => e.AccountSetKey).HasMaxLength(200);
        builder.Property(e => e.AccountSetDisplay).HasMaxLength(1000);
        builder.Property(e => e.InvoiceNumber).HasMaxLength(30);
        builder.Property(e => e.LendingAmount).Monto().IsRequired();
        builder.Property(e => e.AccountingAmount).Monto().IsRequired();
        builder.Property(e => e.Difference).Monto().IsRequired();
        builder.Property(e => e.AcceptanceReason).HasMaxLength(1000);

        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AcceptedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.SessionItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadId, e.CheckVersion, e.DimensionKey }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationLoadDifferences_Load_Version_Dim");
    }
}
