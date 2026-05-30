using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.RemoveRole;

/// <summary>
/// Quita un rol previamente asignado a un usuario (soft-delete del vínculo).
/// Si el vínculo no existe o ya estaba removido, devuelve
/// <c>Security.Users.NotAssignedRole</c>.
/// </summary>
public sealed record RemoveRoleCommand(
    Guid UserPublicId,
    Guid RolePublicId) : IRequest<Result>;
