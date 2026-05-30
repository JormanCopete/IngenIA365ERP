using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.ListUsers;

/// <summary>
/// Lista paginada de usuarios del tenant actual. Filtros opcionales:
///  - <c>Search</c>: substring (case-insensitive) en Username/Email.
///  - <c>IncludeDisabled</c>: si false (default), excluye soft-deleted.
///  - <c>RoleCode</c>: solo usuarios con el rol indicado.
/// </summary>
public sealed record ListUsersQuery(
    string? Search,
    bool IncludeDisabled,
    string? RoleCode,
    PageRequest Paging) : IRequest<Result<PagedResult<UserListItemDto>>>;
