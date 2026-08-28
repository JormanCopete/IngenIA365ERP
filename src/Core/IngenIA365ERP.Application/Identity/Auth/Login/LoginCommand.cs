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
    IReadOnlyList<TenantSummary>? TenantsRequiringMfa = null,

    // Feature 003 (FR-110): presente solo tras canjear un recovery code en mfa/verify.
    int? RecoveryCodesRemaining = null,

    /// <summary>
    /// Con MfaEnrollmentRequired: qué métodos le servirían, como literales
    /// (<c>"Totp"</c>, <c>"WebAuthn"</c>).
    ///
    /// <para>
    /// Sin esto la pantalla de inscripción ofrece los dos botones, y quien pulse el
    /// que su cooperativa no acepta vuelve a quedar fuera — con el agravante de que
    /// ahora cree que ya lo resolvió. Van como literales y no como número porque el
    /// cliente ya sabe leer esos mismos literales en la lista de credenciales, y un
    /// entero mágico por JSON no tiene precedente en este contrato.
    /// </para>
    /// </summary>
    IReadOnlyList<string>? MetodosAceptados = null);

/// <param name="AdmiteTuMetodo">
/// Si esta cooperativa acepta el método con el que la persona acaba de entrar.
/// Se muestra en vez de esconder la fila: una cooperativa a la que pertenece y
/// que desaparece de la lista sin explicación es peor que una que aparece
/// atenuada diciendo por qué. Por defecto <c>true</c> — los demás sitios que
/// construyen este resumen (la lista de sesiones, <c>/me</c>) no están decidiendo
/// un ingreso y no tienen método contra el que comparar.
/// </param>
public sealed record ActiveTenantSummary(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin,
    bool AdmiteTuMetodo = true);

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
