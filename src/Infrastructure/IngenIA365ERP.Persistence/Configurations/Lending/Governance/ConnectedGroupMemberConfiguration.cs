using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_ConnectedGroupMembers</c> (feature 013, T0213; DT §7.10 C112): hecho. Una persona viva a lo sumo una vez por grupo
/// (único <c>(GroupId, PersonId)</c> filtrado <c>[EndedAt] IS NULL</c>).
/// </summary>
public class ConnectedGroupMemberConfiguration : IEntityTypeConfiguration<ConnectedGroupMember>
{
    public const string Tabla = "LND_ConnectedGroupMembers";

    public void Configure(EntityTypeBuilder<ConnectedGroupMember> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.EndedAt);

        builder.HasOne<ConnectedGroup>().WithMany().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.GroupId, e.PersonId }).IsUnique()
            .HasDatabaseName("UK_LND_ConnectedGroupMembers_Group_Person").HasFilter("[EndedAt] IS NULL");
    }
}
