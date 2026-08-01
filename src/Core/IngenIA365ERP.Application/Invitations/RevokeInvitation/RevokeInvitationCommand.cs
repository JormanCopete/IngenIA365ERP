using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Invitations.RevokeInvitation;

/// <summary>
/// T055 — Revoca una invitación pendiente. El emisor puede ser:
/// <list type="bullet">
///   <item>El master admin global (siempre permitido).</item>
///   <item>Un tenant admin <b>del mismo tenant que emitió la invitación</b>.</item>
/// </list>
/// Cualquier otro caller recibe <c>Invitation.Forbidden</c>.
/// </summary>
public sealed record RevokeInvitationCommand(
    Guid InvitationPublicId
) : IRequest<Result>;
