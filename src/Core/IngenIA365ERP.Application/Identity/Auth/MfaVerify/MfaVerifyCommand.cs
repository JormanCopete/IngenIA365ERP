using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Login;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Auth.MfaVerify;

/// <summary>
/// T067 — Segundo factor del login (TOTP). El endpoint
/// <c>POST /api/auth/mfa/verify</c> recibe el challengeToken con
/// <c>purpose=mfa-verify</c> en el header <c>Authorization: Bearer</c> y
/// solo el <see cref="Code"/> en el body. El JWT bearer middleware ya validó
/// firma+expiry+audience; el handler lee el contexto del usuario via
/// <c>ICurrentCentralUserContext</c>.
///
/// <para>
/// Devuelve un <see cref="LoginResult"/> (mismo tagged union que login)
/// porque después de la verificación el resultado puede ser autoSelected o
/// TenantSelection — la UI no necesita ramas adicionales para MFA exitosa.
/// </para>
/// </summary>
public sealed record MfaVerifyCommand(
    string Code,
    bool UseRecoveryCode = false,
    string? IpAddress = null,
    string? UserAgent = null
) : IRequest<Result<LoginResult>>;
