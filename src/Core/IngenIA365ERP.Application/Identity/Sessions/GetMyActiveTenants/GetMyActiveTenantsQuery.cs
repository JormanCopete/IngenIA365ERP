using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Sessions.GetMyActiveTenants;

/// <summary>
/// T082 — Lista de empresas con membresía Active del usuario, con el flag
/// <c>IsDefault</c> resuelto contra <c>CentralUser.DefaultTenantId</c>.
/// Usado por <c>SelectTenant.razor</c> cuando hay >1 membresía.
/// </summary>
public sealed record GetMyActiveTenantsQuery() : IRequest<Result<IReadOnlyList<MyActiveTenantInfo>>>;

public sealed record MyActiveTenantInfo(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin,
    bool IsDefault);
