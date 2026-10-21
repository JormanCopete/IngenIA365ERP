using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_SessionAttendances</c> (feature 013, T0213; data-model §3.9): único <c>(SessionId, PersonId)</c> sin filtro; dos FK al
/// miembro (el que asiste y el principal al que suple).
/// </summary>
public class SessionAttendanceConfiguration : IEntityTypeConfiguration<SessionAttendance>
{
    public const string Tabla = "LND_SessionAttendances";

    public void Configure(EntityTypeBuilder<SessionAttendance> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ArrivedAt);
        builder.Property(e => e.LeftAt);

        builder.HasOne<Session>().WithMany().HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoverningBodyMember>().WithMany().HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoverningBodyMember>().WithMany().HasForeignKey(e => e.SubstitutesMemberId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SessionId, e.PersonId }).IsUnique().HasDatabaseName("UK_LND_SessionAttendances_Session_Person");
    }
}
