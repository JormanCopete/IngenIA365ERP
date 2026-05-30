using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.RestoreUser;

/// <summary>
/// Restaura un usuario previamente soft-deleted (FR-029). Solo aplica si
/// el usuario está actualmente <c>IsDeleted=true</c>.
/// </summary>
public sealed record RestoreUserCommand(Guid UserPublicId) : IRequest<Result>;
