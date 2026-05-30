using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Roles.DeleteRole;

/// <summary>
/// Soft-delete de un rol personalizado. Reglas:
///  - Built-in <c>CompanyAdmin</c> y <c>Auditor</c> nunca se pueden eliminar
///    (FR-020). Otros built-in (Operator, ReadOnly) tampoco para mantener
///    consistencia.
///  - Si hay usuarios con el rol asignado activamente, se rechaza con
///    <c>Security.Roles.CannotDeleteWithUsers</c> — primero hay que quitarles
///    el rol vía RemoveRoleCommand.
/// </summary>
public sealed record DeleteRoleCommand(Guid RolePublicId) : IRequest<Result>;
