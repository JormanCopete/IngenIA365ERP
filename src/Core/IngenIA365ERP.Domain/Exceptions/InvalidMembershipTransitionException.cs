using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Domain.Exceptions;

/// <summary>Lanzada cuando se intenta una transición de membresía no permitida por la máquina de estados (FR-009a).</summary>
public sealed class InvalidMembershipTransitionException : InvalidOperationException
{
    public MembershipStatus From { get; }
    public MembershipStatus To { get; }

    public InvalidMembershipTransitionException(MembershipStatus from, MembershipStatus to)
        : base($"Transición de membresía inválida: {from} → {to}.")
    {
        From = from;
        To = to;
    }
}
