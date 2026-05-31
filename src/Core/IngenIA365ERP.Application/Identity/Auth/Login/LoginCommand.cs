using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Auth.Login;

/// <summary>
/// T066 — Login centralizado de Feature 002. NO recibe tenant: el comando
/// solo valida credenciales contra la identidad central y decide a dónde
/// enrutar al usuario según sus membresías (FR-014 a FR-018).
///
/// <para>
/// La respuesta <see cref="LoginResult"/> es un tagged union: lleva un
/// <c>Challenge</c> string que indica qué pantalla debe mostrar la UI a
/// continuación. Cuando <c>Challenge == "None"</c> y <c>AutoSelected == true</c>,
/// la respuesta YA INCLUYE el access+refresh token operativos.
/// </para>
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password,
    string? IpAddress = null,
    string? UserAgent = null
) : IRequest<Result<LoginResult>>;

/// <summary>
/// Resultado del login. Discriminado por <see cref="Challenge"/> string —
/// los campos no aplicables se dejan null (matchea exactamente
/// <c>contracts/auth.md</c>).
/// </summary>
/// <param name="Challenge">
/// <c>None</c> (autoSelected exitoso) | <c>NoActiveMembership</c> | <c>MfaRequired</c>
/// | <c>MfaEnrollmentRequired</c> | <c>TenantSelection</c>.
/// </param>
public sealed record LoginResult(
    string Challenge,

    // Datos del CentralUser (presentes salvo en NoActiveMembership o errores).
    Guid? CentralUserId = null,
    string? Email = null,
    bool? IsGlobalMasterAdmin = null,

    // Tokens operativos (presentes cuando Challenge=None && AutoSelected).
    string? AccessToken = null,
    DateTime? AccessTokenExpiresAt = null,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAt = null,

    // Challenge token (presente para MfaRequired / MfaEnrollmentRequired / TenantSelection).
    string? ChallengeToken = null,
    string? ChallengeTokenPurpose = null,
    int? ExpiresInSeconds = null,

    // Tenants para TenantSelection / autoSelected.
    IReadOnlyList<ActiveTenantSummary>? ActiveTenants = null,
    Guid? DefaultTenantPublicId = null,
    bool? AutoSelected = null,

    // Detalle del tenant elegido (cuando AutoSelected).
    Guid? ActiveTenantPublicId = null,
    string? ActiveTenantName = null,

    // NoActiveMembership / mensaje libre.
    string? Message = null,

    // MfaEnrollmentRequired: tenants que forzaron el enrollment.
    IReadOnlyList<TenantSummary>? TenantsRequiringMfa = null);

public sealed record ActiveTenantSummary(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin);

public sealed record TenantSummary(
    Guid TenantPublicId,
    string TenantName);

/// <summary>Constantes del campo <see cref="LoginResult.Challenge"/>.</summary>
public static class LoginChallenges
{
    public const string None = "None";
    public const string NoActiveMembership = "NoActiveMembership";
    public const string MfaRequired = "MfaRequired";
    public const string MfaEnrollmentRequired = "MfaEnrollmentRequired";
    public const string TenantSelection = "TenantSelection";
}
