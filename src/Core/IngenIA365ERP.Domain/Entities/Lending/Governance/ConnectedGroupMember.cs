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

    // ------------------------------------------------------------------------------------------- comportamiento (T0393) --

    /// <summary>Vigente el <paramref name="fecha"/>: entró y no salió antes de que acabe ese día (C112: <c>EndedAt</c> ≤ D no cuenta).</summary>
    public bool VigenteEn(DateOnly fecha) => !IsDeleted && ValidFrom <= fecha && (EndedAt is not { } fin || DateOnly.FromDateTime(fin) > fecha);

    public static ConnectedGroupMember Nuevo(int groupId, int personId, DateOnly desde) => new() { GroupId = groupId, PersonId = personId, ValidFrom = desde };

    /// <summary>Retira a la persona del grupo, una sola vez (<see cref="EscrituraUnicaAttribute"/>); el motivo queda en la auditoría del comando.</summary>
    public void Retirar(DateTime instante)
    {
        if (EndedAt is not null) throw new InvalidOperationException("La persona ya salió del grupo.");
        EndedAt = instante;
    }
}
