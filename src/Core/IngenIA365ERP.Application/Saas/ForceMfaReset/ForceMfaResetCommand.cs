using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Saas.ForceMfaReset;

/// <summary>
/// T114 — Reset MFA forzado por el master admin como medida de recuperación
/// operativa (usuario perdió app/dispositivo). Razón obligatoria — queda en
/// audit log. Internamente:
/// - <c>ICentralIdentityProvider.ResetMfaAsync</c> limpia MfaSecret + flag +
///   regenera security stamp (invalida refresh tokens).
/// - Si el tenant exige MFA, el siguiente login pasa por enrollment forzado.
/// </summary>
public sealed record ForceMfaResetCommand(
    Guid CentralUserPublicId,
    string Reason
) : IRequest<Result>;
