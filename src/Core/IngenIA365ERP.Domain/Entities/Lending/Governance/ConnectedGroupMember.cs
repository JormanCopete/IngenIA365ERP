using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_ConnectedGroupMembers</c> (feature 013, T0213; DT §7.10 C112; FR-041, SC-022): una persona miembro de un grupo conectado.
/// Hecho (<see cref="IHechoInmutable"/>): retirarla escribe <see cref="EndedAt"/>; una persona está a lo sumo una vez viva en el grupo.
/// </summary>
public class ConnectedGroupMember : AuditableEntity, IHechoInmutable
{
    public int GroupId { get; init; }

    public int PersonId { get; init; }

    public DateOnly ValidFrom { get; init; }

    [EscrituraUnica]
    public DateTime? EndedAt { get; set; }
}
