using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_RelatedParties</c> (feature 013, T0213; data-model §3.12 y C118): índices por la persona y por la persona del vínculo;
/// porcentaje de aportes como fracción.
/// </summary>
public class RelatedPartyConfiguration : IEntityTypeConfiguration<RelatedParty>
{
    public const string Tabla = "LND_RelatedParties";

    public void Configure(EntityTypeBuilder<RelatedParty> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Basis).HasConversion<int>().IsRequired();
        builder.Property(e => e.KinshipKind).HasConversion<int?>();
        builder.Property(e => e.KinshipDegree);
        builder.Property(e => e.ContributionPercentage).Fraccion();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Source).HasConversion<int>().IsRequired();
        builder.Property(e => e.SupportAttachmentPublicId);
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.EndReason).HasMaxLength(300);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.RelatedThroughPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EndedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_LND_RelatedParties_PersonId");
        builder.HasIndex(e => e.RelatedThroughPersonId).HasDatabaseName("IX_LND_RelatedParties_RelatedThroughPersonId");
    }
}
