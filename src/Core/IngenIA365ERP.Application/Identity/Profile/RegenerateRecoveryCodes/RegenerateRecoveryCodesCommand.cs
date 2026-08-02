using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.RegenerateRecoveryCodes;

/// <summary>
/// Feature 003 (FR-111) — Regenera el juego completo de recovery codes del
/// usuario autenticado. Invalida todos los anteriores. Requiere MFA activo y
/// confirmación de identidad: la contraseña actual O un TOTP vigente
/// (exactamente uno de los dos).
/// </summary>
public sealed record RegenerateRecoveryCodesCommand(
    string? CurrentPassword = null,
    string? TotpCode = null)
    : IRequest<Result<RegenerateRecoveryCodesResult>>;

public sealed record RegenerateRecoveryCodesResult(
    IReadOnlyList<string> RecoveryCodes);
