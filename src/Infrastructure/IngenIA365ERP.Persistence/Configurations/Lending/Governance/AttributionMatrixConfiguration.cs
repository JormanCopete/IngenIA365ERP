using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_AttributionMatrices</c> (feature 013, T0213; data-model §3.1): hecho (C42). Único <c>VersionNumber</c> sin filtro; FK al
/// punto de acta que la aprobó y al usuario que la registró.
/// </summary>
public class AttributionMatrixConfiguration : IEntityTypeConfiguration<AttributionMatrix>
{
    public const string Tabla = "LND_AttributionMatrices";

    public void Configure(EntityTypeBuilder<AttributionMatrix> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.LegalSource).HasMaxLength(300);
        builder.Property(e => e.ContentSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();

        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.SessionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.VersionNumber).IsUnique().HasDatabaseName("UK_LND_AttributionMatrices_Version");
    }
}
