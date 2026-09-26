using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IAutoridadDeOtroAprobador"/> (feature 012, T33, T085): permiso y alcance del aprobador presente en el
/// equipo de quien pidió, que no es quien tiene la sesión. El permiso se resuelve como el de la puerta
/// (<see cref="IUserPermissionResolver.ResolveForTenantAsync"/> en la cooperativa de la petición); el alcance, como el
/// de la petición (<see cref="AlcanceDeInventarioDeLaPeticion"/>, T089): total con <c>Inventory.Scope.AllWarehouses</c> /
/// <c>Inventory.Scope.AllPointsOfSale</c>, y si no, sus asignaciones por los puertos (<see cref="IAsignacionesDeBodega"/>,
/// <see cref="IAsignacionesDePuntoDeVenta"/>); sin ellas, vacío: falla cerrado.
/// </summary>
internal sealed class AutoridadDeOtroAprobador(
    IHttpContextAccessor accessor,
    PermisosDeLaPeticion permisosDeLaPeticion,
    IUserPermissionResolver resolutor,
    IAsignacionesDeBodega bodegas,
    IAsignacionesDePuntoDeVenta puntos) : IAutoridadDeOtroAprobador
{
    public async Task<bool> TienePermisoAsync(int userId, string permiso, CancellationToken ct)
    {
        var concedidos = await PermisosDeAsync(userId, ct);
        return concedidos.Contains(permiso, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AlcanceDeInventario> AlcanceAsync(int userId, CancellationToken ct)
    {
        var concedidos = await PermisosDeAsync(userId, ct);
        var todasLasBodegas = concedidos.Contains(AlcanceDeInventarioDeLaPeticion.TodasLasBodegas, StringComparer.OrdinalIgnoreCase);
        var todosLosPuntos = concedidos.Contains(AlcanceDeInventarioDeLaPeticion.TodosLosPuntos, StringComparer.OrdinalIgnoreCase);
        return AlcanceDeInventario.De(
            todasLasBodegas, await bodegas.BodegasDelUsuarioAsync(userId, ct),
            todosLosPuntos, await puntos.PuntosDelUsuarioAsync(userId, ct));
    }

    private async Task<IReadOnlyList<string>> PermisosDeAsync(int userId, CancellationToken ct)
    {
        if (accessor.HttpContext is not { } http) return [];
        var (_, cooperativa) = await permisosDeLaPeticion.ResolverUsuarioYCooperativaAsync(http, ct);
        return cooperativa is { } id ? await resolutor.ResolveForTenantAsync(userId, id, ct) : [];
    }
}
