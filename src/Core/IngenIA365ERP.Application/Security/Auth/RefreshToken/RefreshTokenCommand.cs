using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.RefreshToken;

/// <summary>
/// Rota el access + refresh. Detecta reuso: si llega un refresh ya rotado,
/// invalida toda la familia y emite notificación <c>SuspiciousSessionActivity</c>.
/// </summary>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<AuthTokensResult>>;
