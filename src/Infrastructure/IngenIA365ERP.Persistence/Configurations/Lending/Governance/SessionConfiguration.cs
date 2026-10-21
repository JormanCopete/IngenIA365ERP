using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_Sessions</c> (feature 013, T0213; data-model §3.8): documento con estado. Número del acta único entre los asignados
/// (<c>[Number] IS NOT NULL</c>); FK al órgano, a la regla sellada y a quien cerró.
/// </summary>
public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public const string Tabla = "LND_Sessions";

    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20);
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Mode).HasConversion<int>().IsRequired();
        builder.Property(e => e.HeldOn).IsRequired();
        builder.Property(e => e.StartedAt);
        builder.Property(e => e.EndedAt);
        builder.Property(e => e.Location).HasMaxLength(200);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Notes);
        builder.Property(e => e.ClosedAt);
        builder.Property(e => e.ContentSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.MinutesAttachmentPublicId);

        builder.HasOne<GoverningBody>().WithMany().HasForeignKey(e => e.GoverningBodyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoverningBodyRuleVersion>().WithMany().HasForeignKey(e => e.RuleVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_Sessions_Number").HasFilter("[Number] IS NOT NULL");
    }
}
