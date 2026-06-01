using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Sessions.SwitchTenant;

/// <summary>
/// T081 — Cambia el tenant activo de una sesión ya autenticada (purpose=full),
/// sin re-login. A diferencia de <c>SelectTenantCommand</c>:
/// (a) requiere JWT con <c>active_tenant_id</c> ya seteado;
/// (b) valida política MFA del tenant destino — si exige MFA y el usuario no
///     la tiene, rechaza con <c>Tenant.MfaPolicyEnforced</c>.
/// </summary>
public sealed record SwitchTenantCommand(
    Guid TenantPublicId
) : IRequest<Result<SwitchTenantResult>>;

public sealed record SwitchTenantResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    int ExpiresInSeconds,
    SwitchedTenantInfo Tenant);

public sealed record SwitchedTenantInfo(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin);
