using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.ResetPassword;

/// <summary>
/// T079f — Consume el token de un solo uso del flujo "olvidé mi contraseña"
/// y aplica la nueva contraseña. Lock distribuido por hash protege contra
/// clicks simultáneos del enlace; el UPDATE condicional sobre RowVersion
/// es la última línea de defensa.
/// </summary>
public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword
) : IRequest<Result>;
