using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_SessionItems</c> (feature 013, T0213; data-model §3.10): único <c>(SessionId, Order)</c> sin filtro; FK al órgano al que
/// recomienda y a la decisión escrita al cerrar.
/// </summary>
public class SessionItemConfiguration : IEntityTypeConfiguration<SessionItem>
{
    public const string Tabla = "LND_SessionItems";

    public void Configure(EntityTypeBuilder<SessionItem> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Rule).HasConversion<int>().IsRequired();
        builder.Property(e => e.SubjectType).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SubjectPublicId).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Summary);
        builder.Property(e => e.ExcludedPersons);
        builder.Property(e => e.Outcome).HasConversion<int?>();
        builder.Property(e => e.Conditions);
        builder.Property(e => e.EvaluationExplanation);

        builder.HasOne<Session>().WithMany().HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoverningBody>().WithMany().HasForeignKey(e => e.RecommendsToBodyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.DecisionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SessionId, e.Order }).IsUnique().HasDatabaseName("UK_LND_SessionItems_Session_Order");
    }
}
