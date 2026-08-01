using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Sessions.SelectTenant;

/// <summary>
/// Cierra el flujo cuando el login devolvió challenge=TenantSelection. El
/// cliente envía el <c>challengeToken</c> (purpose=tenant-select) en el
/// header Authorization y elige una empresa en el body. Respuesta: tokens
/// operativos (purpose=full) con <c>active_tenant_id</c> resuelto.
/// </summary>
public sealed record SelectTenantCommand(
    Guid TenantPublicId
) : IRequest<Result<SelectTenantResult>>;

public sealed record SelectTenantResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    int ExpiresInSeconds,
    SelectedTenantInfo Tenant);

public sealed record SelectedTenantInfo(
    Guid TenantPublicId,
    string TenantName);
