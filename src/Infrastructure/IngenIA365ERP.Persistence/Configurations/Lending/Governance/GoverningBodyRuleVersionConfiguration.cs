using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodyRuleVersions</c> (feature 013, T0213; data-model §3.6; C44): hecho. Único <c>(GoverningBodyId, VersionNumber)</c>
/// sin filtro; fracciones en enteros.
/// </summary>
public class GoverningBodyRuleVersionConfiguration : IEntityTypeConfiguration<GoverningBodyRuleVersion>
{
    public const string Tabla = "LND_GoverningBodyRuleVersions";

    public void Configure(EntityTypeBuilder<GoverningBodyRuleVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.QuorumNumerator).IsRequired();
        builder.Property(e => e.QuorumDenominator).IsRequired();
        builder.Property(e => e.QuorumComparison).HasConversion<int>().IsRequired();
        builder.Property(e => e.MajorityNumerator).IsRequired();
        builder.Property(e => e.MajorityDenominator).IsRequired();
        builder.Property(e => e.MajorityComparison).HasConversion<int>().IsRequired();
        builder.Property(e => e.MajorityBase).HasConversion<int>().IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasOne<GoverningBody>().WithMany().HasForeignKey(e => e.GoverningBodyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.GoverningBodyId, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_LND_GoverningBodyRuleVersions_Body_Version");
    }
}
