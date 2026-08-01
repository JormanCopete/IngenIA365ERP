using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Auth.Logout;

/// <summary>
/// T069 — Cierre de sesión. Elimina el refresh token actual de Redis para
/// evitar refresh post-logout. NO invalida el access token (vence solo en
/// minutos por su TTL corto). Si se requiere invalidar refresh ANTES de
/// vencer, el cliente puede solicitarlo aquí pasando el token.
/// </summary>
public sealed record LogoutCommand(
    string? RefreshToken = null
) : IRequest<Result>;
