using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Roles.CreateRole;

/// <summary>
/// Crea un rol personalizado dentro del tenant actual. Reglas:
///  - <see cref="Code"/> único por tenant (case-insensitive).
///  - <see cref="PermissionPublicIds"/> deben existir todos en el catálogo
///    <c>SEC_Permissions</c>.
///  - No se permite reutilizar un código de built-in (CompanyAdmin/Auditor/Operator/ReadOnly).
///  - El rol creado nunca es built-in (<c>IsBuiltIn=false</c>) — los built-in
///    solo se generan vía seed.
/// </summary>
public sealed record CreateRoleCommand(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<Guid> PermissionPublicIds) : IRequest<Result<Guid>>;
