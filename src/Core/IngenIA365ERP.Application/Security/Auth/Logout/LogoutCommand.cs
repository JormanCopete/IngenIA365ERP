using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;
