using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.AssignRole;

/// <summary>
/// Asigna un rol a un usuario. Idempotente: si ya está asignado activo,
/// devuelve <c>Security.Users.AlreadyAssignedRole</c>. Invalida el cache
/// de permisos efectivos del usuario (FR-019) y notifica al destinatario.
/// </summary>
public sealed record AssignRoleCommand(
    Guid UserPublicId,
    Guid RolePublicId) : IRequest<Result>;
