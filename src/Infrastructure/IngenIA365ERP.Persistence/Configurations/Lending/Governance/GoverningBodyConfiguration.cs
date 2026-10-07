using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodies</c> (feature 013, T0213; data-model §3.5): catálogo; código único entre vivos (<c>CodigoDeCatalogo</c>, 10).
/// </summary>
public class GoverningBodyConfiguration : IEntityTypeConfiguration<GoverningBody>
{
    public const string Tabla = "LND_GoverningBodies";

    public void Configure(EntityTypeBuilder<GoverningBody> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.DecidesCredit).IsRequired();
        builder.Property(e => e.MinutesPrefix).HasMaxLength(10).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_GoverningBodies_Code").HasFilter("[IsDeleted] = 0");
    }
}
