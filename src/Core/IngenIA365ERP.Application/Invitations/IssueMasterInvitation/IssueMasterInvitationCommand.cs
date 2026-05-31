using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Invitations.IssueMasterInvitation;

/// <summary>
/// T052 — Emite una invitación a una empresa desde el master admin del SaaS.
/// A diferencia de <c>IssueTenantInvitationCommand</c>, este command permite
/// promover al destinatario a tenant admin de la empresa invitante mediante
/// <see cref="InviteAsTenantAdmin"/> (FR-025, FR-026 — el master es la única
/// fuente que puede crear nuevos tenant admins).
/// </summary>
public sealed record IssueMasterInvitationCommand(
    Guid TenantPublicId,
    string Email,
    bool InviteAsTenantAdmin
) : IRequest<Result<IssueMasterInvitationResult>>;

public sealed record IssueMasterInvitationResult(
    Guid InvitationPublicId,
    DateTime ExpiresAt,
    bool InviteAsTenantAdmin
);
