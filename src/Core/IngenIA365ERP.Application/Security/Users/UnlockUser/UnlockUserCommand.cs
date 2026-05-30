using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.UnlockUser;

/// <summary>
/// Limpia <c>LockoutEndAt</c> y resetea <c>FailedLoginAttempts</c>.
/// No cambia la contraseña ni el estado IsActive.
/// </summary>
public sealed record UnlockUserCommand(Guid UserPublicId) : IRequest<Result>;
