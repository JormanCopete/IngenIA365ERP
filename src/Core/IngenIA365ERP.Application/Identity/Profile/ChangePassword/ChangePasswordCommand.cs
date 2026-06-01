using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.ChangePassword;

/// <summary>
/// T079d — Cambia la contraseña del usuario autenticado. Requiere validar
/// el password actual + el nuevo NO está en Pwned (FR-044). Al éxito,
/// regenera el SecurityStamp → invalida todos los refresh tokens del usuario.
/// Envía notificación de seguridad por correo (fail-soft).
/// </summary>
public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string? IpAddress = null,
    string? UserAgent = null
) : IRequest<Result>;
