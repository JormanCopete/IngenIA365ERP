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
    bool InviteAsTenantAdmin,
    string? DocumentType = null,
    string? DocumentNumber = null,
    string? DocumentNumberConfirmation = null,
    string? FirstName = null,
    string? OtherNames = null,
    string? LastName = null,
    string? SecondLastName = null,
    string? MobilePhone = null,
    Guid? RolePublicId = null,
    Guid? PositionPublicId = null
) : IRequest<Result<IssueMasterInvitationResult>>
{
    public Services.DatosDelInvitado Datos() => new(
        Email, DocumentType, DocumentNumber, DocumentNumberConfirmation,
        FirstName, OtherNames, LastName, SecondLastName, MobilePhone, RolePublicId, PositionPublicId);
}

public sealed record IssueMasterInvitationResult(
    Guid InvitationPublicId,
    DateTime ExpiresAt,
    bool InviteAsTenantAdmin,
    string? Message = null
);
