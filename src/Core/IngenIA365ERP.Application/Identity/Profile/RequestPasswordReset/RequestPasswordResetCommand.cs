using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.RequestPasswordReset;

/// <summary>
/// T079e — "Olvidé mi contraseña". Si el email existe, genera token de un
/// solo uso + envía correo con enlace. SIEMPRE devuelve éxito (202)
/// incluso cuando el email no existe — defensa anti-enumeración (FR-041).
/// El audit log SÍ distingue ambos casos para análisis interno.
/// </summary>
public sealed record RequestPasswordResetCommand(
    string Email,
    string? IpAddress = null
) : IRequest<Result>;
