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
/// Resultado de la aceptación: JWT access + refresh con <c>active_tenant_id</c>
/// ya seteado al tenant invitante, listos para sustituir al token actual del
/// cliente. <c>CentralUserId</c> y <c>ActiveTenantPublicId</c> se devuelven
/// para que la UI pueda enrutar al dashboard correcto.
/// </summary>
public sealed record AcceptInvitationResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    Guid CentralUserId,
    Guid ActiveTenantPublicId,
    string ActiveTenantName);
