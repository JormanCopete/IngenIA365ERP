using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.UpdateUser;

/// <summary>
/// Actualiza datos demográficos / identificación del usuario. NO toca
/// contraseña ni roles — usar <c>AdminResetPasswordCommand</c> y
/// <c>AssignRoleCommand</c>/<c>RemoveRoleCommand</c> respectivamente.
///
/// El <c>Username</c> NO es editable (es identidad de máquina). El
/// <c>Email</c> sí, pero queda <c>IsEmailVerified=false</c> hasta nueva
/// verificación (no automatizada en Phase 0).
/// </summary>
public sealed record UpdateUserCommand(
    Guid UserPublicId,
    string Email,
    string? IdentificationNumber,
    int? PersonId) : IRequest<Result>;
