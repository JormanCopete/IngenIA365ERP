using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_FactTypes</c> (feature 013, T0210; data-model §1.11): únicos <c>Code</c> y <c>ShortCode</c>, sin filtro (catálogo sembrado).</summary>
public class LendingFactTypeConfiguration : IEntityTypeConfiguration<LendingFactType>
{
    public const string Tabla = "LND_FactTypes";

    public void Configure(EntityTypeBuilder<LendingFactType> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ShortCode).HasMaxLength(4).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.VoucherTypeCode).HasMaxLength(10);
        builder.Property(e => e.DefaultMode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DefaultBatchTrigger).HasMaxLength(20);
        builder.Property(e => e.DeliveredIn).HasConversion<int>().IsRequired();
        builder.Property(e => e.IsSeeded).HasDefaultValue(true);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_FactTypes_Code");
        builder.HasIndex(e => e.ShortCode).IsUnique().HasDatabaseName("UK_LND_FactTypes_ShortCode");
    }
}
