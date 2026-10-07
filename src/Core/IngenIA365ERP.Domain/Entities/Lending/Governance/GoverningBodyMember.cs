using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodyMembers</c> (feature 013, T0213; data-model §3.7; FR-039, FR-040): un miembro del órgano —una persona, no
/// necesariamente usuario (I-1)— con su papel y vigencia. Estado con fechas, no hecho; una persona no tiene dos vigencias cruzadas en
/// el mismo órgano (<c>Lending.GoverningBody.MemberOverlaps</c>).
/// </summary>
public class GoverningBodyMember : AuditableEntity
{
    public int GoverningBodyId { get; set; }

    public int PersonId { get; set; }

    public BodyMemberRole Role { get; set; }

    public bool HasVote { get; set; }

    public bool IsPrincipal { get; set; }

    /// <summary>El principal de quien es suplente.</summary>
    public int? AlternateForMemberId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Nombramiento (acto externo a Cartera).</summary>
    public string? AppointmentNotes { get; set; }

    public Guid? AppointmentAttachmentPublicId { get; set; }
}
