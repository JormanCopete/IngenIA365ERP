using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;

namespace IngenIA365ERP.Application.Security.Roles.ListRoles;

/// <summary>Listado de roles del tenant + roles SaaS-globales (TenantId = null).</summary>
public sealed record ListRolesQuery(
    string? Search,
    bool IncludeBuiltIn,
    PageRequest Paging) : IRequest<Result<PagedResult<RoleListItemDto>>>;

public sealed record RoleListItemDto(
    Guid PublicId,
    string Code,
    string Name,
    string? Description,
    bool IsBuiltIn,
    bool IsAssignable,
    bool IsActive,
    int PermissionCount,
    int UserCount);
