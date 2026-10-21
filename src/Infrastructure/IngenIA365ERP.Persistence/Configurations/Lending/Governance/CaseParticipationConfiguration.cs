using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_CaseParticipations</c> (feature 013, T0213; data-model §3.13): hecho. Índices <c>(CaseRootPublicId, Action)</c> y
/// <c>(PersonId, CaseRootPublicId)</c> para que <c>ReglasDeSegregacion</c> busque la acción previa.
/// </summary>
public class CaseParticipationConfiguration : IEntityTypeConfiguration<CaseParticipation>
{
    public const string Tabla = "LND_CaseParticipations";

    public void Configure(EntityTypeBuilder<CaseParticipation> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.CaseRootType).HasMaxLength(40).IsRequired();
        builder.Property(e => e.CaseRootPublicId).IsRequired();
        builder.Property(e => e.SubjectType).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SubjectPublicId).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(30).IsRequired();
        builder.Property(e => e.At).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CaseRootPublicId, e.Action }).HasDatabaseName("IX_LND_CaseParticipations_Root_Action");
        builder.HasIndex(e => new { e.PersonId, e.CaseRootPublicId }).HasDatabaseName("IX_LND_CaseParticipations_Person_Root");
    }
}
