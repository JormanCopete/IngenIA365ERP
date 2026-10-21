using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodyMembers</c> (feature 013, T0213; data-model §3.7): estado con fechas. FK al órgano, a la persona y al principal
/// del que es suplente (autorreferencia).
/// </summary>
public class GoverningBodyMemberConfiguration : IEntityTypeConfiguration<GoverningBodyMember>
{
    public const string Tabla = "LND_GoverningBodyMembers";

    public void Configure(EntityTypeBuilder<GoverningBodyMember> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Role).HasConversion<int>().IsRequired();
        builder.Property(e => e.HasVote).IsRequired();
        builder.Property(e => e.IsPrincipal).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.AppointmentNotes).HasMaxLength(300);
        builder.Property(e => e.AppointmentAttachmentPublicId);

        builder.HasOne<GoverningBody>().WithMany().HasForeignKey(e => e.GoverningBodyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoverningBodyMember>().WithMany().HasForeignKey(e => e.AlternateForMemberId).OnDelete(DeleteBehavior.Restrict);
    }
}
