using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_SessionVotes</c> (feature 013, T0213; data-model §3.11; C75): hecho. Un voto vigente por persona y punto
/// (<c>[ReplacedAt] IS NULL</c>) y cada voto reemplazado a lo sumo una vez (<c>[ReplacesVoteId] IS NOT NULL</c>).
/// </summary>
public class SessionVoteConfiguration : IEntityTypeConfiguration<SessionVote>
{
    public const string Tabla = "LND_SessionVotes";

    public void Configure(EntityTypeBuilder<SessionVote> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Value).HasConversion<int>().IsRequired();
        builder.Property(e => e.Method).HasConversion<int>().IsRequired();
        builder.Property(e => e.PresenceChallengePublicId);
        builder.Property(e => e.RecordedAt).IsRequired();
        builder.Property(e => e.ReplacementReason).HasMaxLength(300);
        builder.Property(e => e.ReplacedAt);

        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoverningBodyMember>().WithMany().HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionVote>().WithMany().HasForeignKey(e => e.ReplacesVoteId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ItemId, e.PersonId }).IsUnique()
            .HasDatabaseName("UK_LND_SessionVotes_Item_Person").HasFilter("[ReplacedAt] IS NULL");
        builder.HasIndex(e => e.ReplacesVoteId).IsUnique()
            .HasDatabaseName("UK_LND_SessionVotes_Replaces").HasFilter("[ReplacesVoteId] IS NOT NULL");
    }
}
