using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_NumberSequences</c> (feature 013, T0210; data-model §1.10): único <c>(Kind, ScopeKey)</c> sin filtro; <c>NextValue</c> bigint.</summary>
public class LendingNumberSequenceConfiguration : IEntityTypeConfiguration<LendingNumberSequence>
{
    public const string Tabla = "LND_NumberSequences";

    public void Configure(EntityTypeBuilder<LendingNumberSequence> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.ScopeKey).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Prefix).HasMaxLength(10).IsRequired();
        builder.Property(e => e.NextValue).IsRequired();
        builder.Property(e => e.MinDigits).IsRequired();

        builder.HasIndex(e => new { e.Kind, e.ScopeKey }).IsUnique().HasDatabaseName("UK_LND_NumberSequences_Kind_Scope");
    }
}
