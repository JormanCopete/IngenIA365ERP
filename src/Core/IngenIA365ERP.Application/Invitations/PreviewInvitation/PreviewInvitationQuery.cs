using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Invitations.PreviewInvitation;

/// <summary>
/// T053 — Lee los detalles de una invitación a partir del token plano del
/// enlace, SIN consumirla. La UI lo invoca al cargar
/// <c>/auth/accept-invitation?token=...</c> para decidir qué pantalla mostrar
/// (registro nuevo, confirmación con password de identidad existente, o un
/// solo clic si la sesión activa coincide con el email invitado — FR-029(b)).
/// </summary>
public sealed record PreviewInvitationQuery(
    string Token
) : IRequest<Result<PreviewInvitationResult>>;

/// <summary>
/// Resultado del preview. <see cref="IsValid"/>=false cuando el token es
/// inexistente, ya consumido, revocado, expirado o superseded — el
/// <see cref="ErrorCode"/> indica la causa exacta para que la UI muestre el
/// mensaje correspondiente.
/// </summary>
public sealed record PreviewInvitationResult(
    Guid TenantPublicId,
    string TenantName,
    string Email,
    bool IsExistingCentralUser,
    bool InviteAsTenantAdmin,
    DateTime ExpiresAt,
    bool IsValid,
    string? ErrorCode);
