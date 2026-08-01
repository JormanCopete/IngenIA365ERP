using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Invitations.AcceptInvitation;

/// <summary>
/// T054 — Acepta una invitación pendiente. La entrada admite 3 ramas
/// MUTUAMENTE EXCLUSIVAS (validadas por <c>AcceptInvitationCommandValidator</c>):
/// <list type="bullet">
///   <item><see cref="Registration"/> — el email aún NO tiene identidad central.
///         El handler crea el <c>CentralUser</c> con la nueva contraseña
///         (verificación Pwned interna).</item>
///   <item><see cref="ExistingCredentials"/> — el email YA tiene identidad
///         central, no hay sesión activa con ese email. El handler valida
///         contraseña y reutiliza la identidad.</item>
///   <item><see cref="UseActiveSession"/> — el caller ya está logueado con
///         JWT central cuyo email coincide con el invitado. El handler
///         omite contraseña (FR-029(b)) tras verificar el match.</item>
/// </list>
/// </summary>
public sealed record AcceptInvitationCommand(
    string Token,
    NewRegistrationInput? Registration = null,
    ExistingCredentialsInput? ExistingCredentials = null,
    bool UseActiveSession = false
) : IRequest<Result<AcceptInvitationResult>>;

/// <summary>
/// Datos del registro nuevo. <c>Password</c> debe cumplir la política
/// (mín. 12 chars, no Pwned — verificada por <c>ICentralIdentityProvider</c>).
/// </summary>
public sealed record NewRegistrationInput(string Password);

/// <summary>Credenciales del CentralUser existente que confirma la invitación.</summary>
public sealed record ExistingCredentialsInput(string Password);

/// <summary>
/// Resultado de la aceptación. La membresía queda SIEMPRE activa, pero la
/// sesión emitida depende de las exigencias de MFA (FR-003b/FR-003c):
/// <list type="bullet">
///   <item><c>Challenge = "None"</c> — access + refresh operativos con
///         <c>active_tenant_id</c> del tenant invitante.</item>
///   <item><c>Challenge = "MfaRequired"</c> — el usuario tiene MFA activo y
///         no lo verificó en este flujo: <c>ChallengeToken</c>
///         (purpose=mfa-verify) para <c>/api/auth/mfa/verify</c>.</item>
///   <item><c>Challenge = "MfaEnrollmentRequired"</c> — el tenant invitante
///         exige MFA y el usuario no lo tiene: <c>ChallengeToken</c>
///         (purpose=mfa-enroll) para el enrollment forzado.</item>
/// </list>
/// </summary>
public sealed record AcceptInvitationResult(
    string? AccessToken,
    DateTime? AccessTokenExpiresAt,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    Guid CentralUserId,
    Guid ActiveTenantPublicId,
    string ActiveTenantName,
    string Challenge = "None",
    string? ChallengeToken = null);
