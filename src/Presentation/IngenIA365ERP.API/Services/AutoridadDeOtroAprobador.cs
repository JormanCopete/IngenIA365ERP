using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IAutoridadDeOtroAprobador"/> (feature 012, T33, T085): permiso y alcance del aprobador presente en el
/// equipo de quien pidió, que no es quien tiene la sesión. El permiso se resuelve como el de la puerta
/// (<see cref="IUserPermissionResolver.ResolveForTenantAsync"/> en la cooperativa de la petición); el alcance, hasta que
/// existan las asignaciones por usuario (T088/T089), es total sólo con <c>Inventory.Scope.AllWarehouses</c> y
/// <c>Inventory.Scope.AllPointsOfSale</c>, y si no, vacío: falla cerrado.
/// </summary>
internal sealed class AutoridadDeOtroAprobador(
    IHttpContextAccessor accessor,
    PermisosDeLaPeticion permisosDeLaPeticion,
    IUserPermissionResolver resolutor) : IAutoridadDeOtroAprobador
{
    public async Task<bool> TienePermisoAsync(int userId, string permiso, CancellationToken ct)
    {
        var concedidos = await PermisosDeAsync(userId, ct);
        return concedidos.Contains(permiso, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AlcanceDeInventario> AlcanceAsync(int userId, CancellationToken ct)
    {
        var concedidos = await PermisosDeAsync(userId, ct);
        var bodegas = concedidos.Contains("Inventory.Scope.AllWarehouses", StringComparer.OrdinalIgnoreCase);
        var puntos = concedidos.Contains("Inventory.Scope.AllPointsOfSale", StringComparer.OrdinalIgnoreCase);
        return AlcanceDeInventario.Vacio with { TodasLasBodegas = bodegas, TodosLosPuntos = puntos };
    }

    private async Task<IReadOnlyList<string>> PermisosDeAsync(int userId, CancellationToken ct)
    {
        if (accessor.HttpContext is not { } http) return [];
        var (_, cooperativa) = await permisosDeLaPeticion.ResolverUsuarioYCooperativaAsync(http, ct);
        return cooperativa is { } id ? await resolutor.ResolveForTenantAsync(userId, id, ct) : [];
    }
}
