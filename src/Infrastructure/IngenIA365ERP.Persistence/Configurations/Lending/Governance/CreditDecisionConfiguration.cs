using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_CreditDecisions</c> (feature 013, T0213; data-model §3.4): hecho. CHECK <c>CK_LND_CreditDecisions_Decider</c> (individual
/// o colegiada, exactamente una) e índice de lectura por sujeto <c>(SubjectType, SubjectPublicId, DecidedAt)</c>.
/// </summary>
public class CreditDecisionConfiguration : IEntityTypeConfiguration<CreditDecision>
{
    public const string Tabla = "LND_CreditDecisions";

    public void Configure(EntityTypeBuilder<CreditDecision> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_CreditDecisions_Decider",
            "([DecidedByUserId] IS NOT NULL AND [SessionItemId] IS NULL) OR ([DecidedByUserId] IS NULL AND [SessionItemId] IS NOT NULL)"));

        builder.Property(e => e.SubjectType).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SubjectPublicId).IsRequired();
        builder.Property(e => e.CaseRootPublicId).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.Conditions);
        builder.Property(e => e.Reason).HasMaxLength(1000);
        builder.Property(e => e.IsPolicyException).IsRequired();
        builder.Property(e => e.DecisionSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.DecidedAt).IsRequired();
        builder.Property(e => e.ValidUntil);

        builder.HasOne<AttributionMatrix>().WithMany().HasForeignKey(e => e.AttributionMatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttributionLevel>().WithMany().HasForeignKey(e => e.LevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.DecidedByPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.SessionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.SupersedesDecisionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SubjectType, e.SubjectPublicId, e.DecidedAt })
            .HasDatabaseName("IX_LND_CreditDecisions_Subject_DecidedAt");
    }
}
