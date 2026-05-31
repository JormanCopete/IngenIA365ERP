using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Invitations.IssueTenantInvitation;

/// <summary>
/// T051 — Emite una invitación a la empresa indicada. El emisor debe ser
/// administrador del tenant (verificado por el handler además del filter
/// <c>RequireTenantAdmin</c> del endpoint). NO se permite marcar el destinatario
/// como tenant admin desde este command — esa potestad es exclusiva del master
/// admin (ver <c>IssueMasterInvitationCommand</c>, T052).
/// </summary>
public sealed record IssueTenantInvitationCommand(
    Guid TenantPublicId,
    string Email
) : IRequest<Result<IssueTenantInvitationResult>>;

/// <summary>
/// Resultado de la emisión: identificador público y vencimiento. El token plano
/// NO se devuelve al API (sale por correo al destinatario, FR-024).
/// </summary>
public sealed record IssueTenantInvitationResult(
    Guid InvitationPublicId,
    DateTime ExpiresAt
);
