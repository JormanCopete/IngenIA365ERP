using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationLoads</c> (feature 013, K1; data-model §29.1): <c>Number</c> único; un borrador, una aplicada y una activada por
/// cooperativa (tres únicos filtrados sobre <c>Status</c>); <c>ReplacedByLoadId</c> se autorreferencia.
/// </summary>
public class MigrationLoadConfiguration : IEntityTypeConfiguration<MigrationLoad>
{
    public const string Tabla = "LND_MigrationLoads";

    public void Configure(EntityTypeBuilder<MigrationLoad> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).IsRequired();
        builder.Property(e => e.CutoffDate).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Description).HasMaxLength(300);
        builder.Property(e => e.CurrentCheckVersion).IsRequired();
        builder.Property(e => e.BalanceCheckJson);
        builder.Property(e => e.BalanceCheckSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.SalesReconciliationSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.ValidationSummaryJson);
        builder.Property(e => e.TotalsJson);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.ReplacedByLoadId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_MigrationLoads_Number");

        builder.HasIndex(e => e.Status, "UK_LND_MigrationLoads_Draft").IsUnique()
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.Status, "UK_LND_MigrationLoads_Applied").IsUnique()
            .HasFilter("[Status] = 2 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.Status, "UK_LND_MigrationLoads_Activated").IsUnique()
            .HasFilter("[Status] = 4 AND [IsDeleted] = 0");
    }
}
