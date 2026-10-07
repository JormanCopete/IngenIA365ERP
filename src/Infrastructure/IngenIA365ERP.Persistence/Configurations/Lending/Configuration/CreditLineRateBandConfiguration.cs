using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_CreditLineRateBands</c> (feature 013, T0210; data-model §1.4): hija de la versión, único <c>(Version, Order)</c>.</summary>
public class CreditLineRateBandConfiguration : IEntityTypeConfiguration<CreditLineRateBand>
{
    public const string Tabla = "LND_CreditLineRateBands";

    public void Configure(EntityTypeBuilder<CreditLineRateBand> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.MinAmount).Monto();
        builder.Property(e => e.MaxAmount).Monto();
        builder.Property(e => e.DebtorKind).HasConversion<int?>();
        builder.Property(e => e.AnnualRate).TasaAnual();
        builder.Property(e => e.Margin).TasaAnual();

        builder.HasOne<CreditLineVersion>().WithMany().HasForeignKey(e => e.CreditLineVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CreditLineVersionId, e.Order }).IsUnique()
            .HasDatabaseName("UK_LND_CreditLineRateBands_Version_Order");
    }
}
