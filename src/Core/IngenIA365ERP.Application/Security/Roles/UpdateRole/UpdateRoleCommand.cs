using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Roles.UpdateRole;

/// <summary>
/// Actualiza un rol existente. Reglas:
///  - El <c>Code</c> NO es editable (es identidad de máquina).
///  - Si el rol es built-in, solo se permite editar <see cref="Name"/>,
///    <see cref="Description"/> y la lista de permisos (no <c>IsAssignable</c>
///    ni el código).
///  - Cambiar la lista de permisos invalida el cache de permisos efectivos
///    de todos los usuarios con este rol (delegado al handler de US2 con T075).
/// </summary>
public sealed record UpdateRoleCommand(
    Guid RolePublicId,
    string Name,
    string? Description,
    bool IsAssignable,
    IReadOnlyList<Guid> PermissionPublicIds) : IRequest<Result>;
