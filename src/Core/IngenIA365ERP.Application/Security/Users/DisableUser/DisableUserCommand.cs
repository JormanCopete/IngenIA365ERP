using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.DisableUser;

/// <summary>
/// Deshabilita un usuario (soft-delete + IsActive=false). Adicionalmente
/// revoca todos sus refresh tokens activos para forzar cierre de sesión
/// inmediato en clientes existentes.
/// </summary>
public sealed record DisableUserCommand(Guid UserPublicId) : IRequest<Result>;
