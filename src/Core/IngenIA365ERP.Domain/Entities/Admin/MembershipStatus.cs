using IngenIA365ERP.Domain.Exceptions;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Estados de la relación entre una identidad central y una empresa (FR-009).
/// Las transiciones válidas están centralizadas en <see cref="MembershipStatusTransitions"/>
/// (FR-009a).
/// </summary>
public enum MembershipStatus
{
    Invited = 0,
    Active = 1,
    Suspended = 2,
    Revoked = 3,
}

/// <summary>
/// Centraliza la máquina de estados de <see cref="MembershipStatus"/>.
/// Las entidades de dominio invocan <see cref="EnsureTransition"/> antes de mutar.
/// La excepción <see cref="InvalidMembershipTransitionException"/> vive en
/// <c>IngenIA365ERP.Domain.Exceptions</c> (fuera de <c>Domain.Entities</c>) para no
/// confundir al architecture test del Principio VII.
/// </summary>
public static class MembershipStatusTransitions
{
    private static readonly HashSet<(MembershipStatus from, MembershipStatus to)> Allowed = new()
    {
        (MembershipStatus.Invited,   MembershipStatus.Active),
        (MembershipStatus.Invited,   MembershipStatus.Revoked),
        (MembershipStatus.Active,    MembershipStatus.Suspended),
        (MembershipStatus.Suspended, MembershipStatus.Active),
        (MembershipStatus.Active,    MembershipStatus.Revoked),
        (MembershipStatus.Suspended, MembershipStatus.Revoked),
        // Revoked → Active es legal SOLO vía aceptación de nueva invitación
        // (AcceptInvitationCommandHandler — fuera de Domain). El check estructural
        // permite la transición; la regla de negocio "requiere nueva invitación" se
        // aplica en Application.
        (MembershipStatus.Revoked,   MembershipStatus.Active),
    };

    public static bool CanTransition(this MembershipStatus from, MembershipStatus to) =>
        Allowed.Contains((from, to));

    public static void EnsureTransition(this MembershipStatus from, MembershipStatus to)
    {
        if (!from.CanTransition(to))
            throw new InvalidMembershipTransitionException(from, to);
    }
}
