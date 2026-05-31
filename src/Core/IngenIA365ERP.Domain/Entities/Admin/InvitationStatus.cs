namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>Estados de una invitación. Terminales: Accepted, Expired, Revoked.</summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Expired = 2,
    Revoked = 3,
}
