using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_CommercialTermEquivalences</c> (feature 013, T0210; data-model §1.9): único <c>(Kind, CommercialCode, VersionNumber)</c>.</summary>
public class CommercialTermEquivalenceConfiguration : IEntityTypeConfiguration<CommercialTermEquivalence>
{
    public const string Tabla = "LND_CommercialTermEquivalences";

    public void Configure(EntityTypeBuilder<CommercialTermEquivalence> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.CommercialCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Periodicity).HasConversion<int?>();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();

        builder.HasIndex(e => new { e.Kind, e.CommercialCode, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_LND_CommercialTermEquivalences_Kind_Code_Version");
    }
}
