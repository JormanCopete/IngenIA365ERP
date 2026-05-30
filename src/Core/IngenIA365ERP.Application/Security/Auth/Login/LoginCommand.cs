using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.Login;

/// <summary>
/// Paso 1 del flujo de autenticación (FR-005). Valida credenciales contra
/// BCrypt y emite un challenge MFA opaco (5 min) o, si el usuario tiene MFA
/// deshabilitado, ejecuta directamente el segundo paso (no por defecto: en
/// este sistema MFA es obligatorio para todos los usuarios reales).
/// </summary>
public sealed record LoginCommand(
    string? TenantSubdomainOrNit,
    string Username,
    string Password,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<LoginChallengeResult>>;
