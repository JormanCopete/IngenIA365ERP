using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.AdminResetPassword;

/// <summary>
/// Reset administrativo de contraseña (FR-009 — el usuario debe cambiarla en
/// su próximo login). La nueva contraseña debe cumplir la política del tenant
/// y no estar en el historial reciente (no-reuso, FR-010).
///
/// Side-effects:
///  - <c>User.MustChangePassword = true</c>
///  - Revoca todos los refresh tokens activos.
///  - Resetea contador de fallos y lockout (el usuario partió de cero).
///  - Notifica al destinatario (correo + in-app) avisando del reset.
/// </summary>
public sealed record AdminResetPasswordCommand(
    Guid UserPublicId,
    string NewPassword) : IRequest<Result>;
