using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Auth.RefreshToken;

/// <summary>
/// T068 — Renueva el access token usando el refresh actual con rotation
/// (un refresh se usa una sola vez; al rotar se genera uno nuevo y el viejo
/// queda marcado). Si llega un refresh ya rotado → reuso detectado → la
/// familia entera se invalida.
/// </summary>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress = null,
    string? UserAgent = null
) : IRequest<Result<RefreshTokenResult>>;

public sealed record RefreshTokenResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    int ExpiresInSeconds);
