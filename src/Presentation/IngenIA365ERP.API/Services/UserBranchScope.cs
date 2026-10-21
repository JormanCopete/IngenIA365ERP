using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Security;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IUserBranchScope"/> para la petición en curso (feature 009, FR-035, R7). Tres
/// saltos: quien opera → sus filas activas en <c>SEC_UserBranchAssignments</c> de la cooperativa
/// activa (oficinas de <c>ADM_Branches</c>) → las sucursales de <c>COR_Branches</c> que las
/// referencian por <c>TenantBranchPublicId</c>. Sin asignaciones no hay restricción; con
/// asignaciones que ninguna sucursal contable referencia, el alcance queda vacío a propósito.
/// El maestro global no tiene alcance.
///
/// <para>
/// Feature 013 (T62, R131, T0284): el salto vive en <see cref="OficinasDelUsuario"/>, que también usa el alcance de Cartera; aquí
/// queda sólo la semántica <b>abierta</b> de Contabilidad (<see cref="AlcanceDeSucursales.De"/>), que no cambia.
/// </para>
/// </summary>
internal sealed class UserBranchScope(
    IHttpContextAccessor accessor,
    PermisosDeLaPeticion peticion,
    OficinasDelUsuario oficinasDelUsuario) : IUserBranchScope
{
    public async Task<AlcanceDeSucursales> ObtenerAsync(CancellationToken ct)
    {
        var http = accessor.HttpContext;
        if (http is null) return AlcanceDeSucursales.SinRestriccion;
        if (RequireMasterAdminAttribute.Check(http).IsAllowed) return AlcanceDeSucursales.SinRestriccion;

        var (usuario, cooperativa) = await peticion.ResolverUsuarioYCooperativaAsync(http, ct);
        if (usuario is null || cooperativa is null) return AlcanceDeSucursales.SinRestriccion;

        return AlcanceDeSucursales.De(await oficinasDelUsuario.LeerAsync(usuario.Value, cooperativa.Value, ct));
    }
}
