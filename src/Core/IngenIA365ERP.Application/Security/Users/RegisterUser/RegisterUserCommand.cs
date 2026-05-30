using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.RegisterUser;

/// <summary>
/// Registra un nuevo usuario en la cooperativa. La contraseña inicial la
/// elige el administrador, pero se marca <c>MustChangePassword=true</c> →
/// el usuario debe cambiarla en su primer login (FR-009).
///
/// MFA queda deshabilitado al crear; el usuario lo inscribe vía
/// <c>EnrollMfaStartCommand</c> tras su primer login.
/// </summary>
public sealed record RegisterUserCommand(
    string Username,
    string Email,
    string? FullName,
    string InitialPassword,
    int? PersonId,
    string? IdentificationNumber,
    IReadOnlyList<Guid> RolePublicIds) : IRequest<Result<Guid>>;
