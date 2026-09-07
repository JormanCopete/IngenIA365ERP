using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Identity.Profile.Common;

/// <summary>
/// Decide si una persona puede quedarse SIN segundo factor.
///
/// <para>
/// Vive aparte porque ahora hay dos caminos que llevan al mismo sitio: desactivar
/// el MFA entero, y revocar la última credencial que queda. Si la regla estuviera
/// duplicada, arreglarla en un sitio y no en el otro dejaría abierta la puerta de
/// al lado — y la puerta de al lado es la que la gente usa a diario.
/// </para>
/// </summary>
internal static class GuardiaDeSegundoFactor
{
    /// <summary>
    /// <see cref="Result.Success()"/> si puede quedarse sin segundo factor; un
    /// fallo con el motivo si no.
    /// </summary>
    /// <param name="esMaestroGlobal">
    /// Sale del token, no de la base: el maestro no tiene membresías por diseño.
    /// </param>
    public static async Task<Result> PuedeQuedarseSinSegundoFactorAsync(
        Guid centralUserId,
        bool esMaestroGlobal,
        ITenantMembershipReader membresias,
        CancellationToken ct)
    {
        // El maestro global NO tiene membresías: gobierna el conjunto de
        // cooperativas y no pertenece a ninguna. La comprobación de abajo recorre
        // sus membresías, no encuentra ninguna exigente, y lo dejaba pasar — o sea
        // que la única cuenta que crea cooperativas y borra el segundo factor de
        // cualquiera era, justamente, la que podía quedarse sin el suyo.
        if (esMaestroGlobal)
        {
            return Result.Failure(
                "Profile.Mfa.RequiredForMasterAdmin",
                "El administrador maestro no puede quedarse sin segundo factor. " +
                "Si perdiste el dispositivo, usá un código de recuperación e inscribí otro antes de retirar éste.");
        }

        var activas = await membresias.GetActiveMembershipsAsync(centralUserId, ct);
        var exigente = activas.FirstOrDefault(m => m.IsMfaRequiredByTenant);

        if (exigente is not null)
        {
            return Result.Failure(
                "Profile.Mfa.RequiredByTenantPolicy",
                $"No podés quedarte sin segundo factor: la empresa '{exigente.TenantName}' lo exige.");
        }

        return Result.Success();
    }
}
