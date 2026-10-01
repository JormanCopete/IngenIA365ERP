using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IAlcanceDeInventario"/> de la API (feature 012, T35, T089; pregunta C5): las bodegas y los puntos de venta
/// de quien hace la petición.
///
/// <list type="bullet">
/// <item><b>Falla cerrado.</b> Sin persona resuelta en <c>SEC_Users</c>, sin asignaciones o ante cualquier duda, el
/// alcance es vacío. El total de cada clase sólo lo dan <c>Inventory.Scope.AllWarehouses</c> y
/// <c>Inventory.Scope.AllPointsOfSale</c> (y el administrador maestro, que la puerta deja pasar por su atajo).</item>
/// <item><b>Lee sólo por los puertos</b> <see cref="IAsignacionesDeBodega"/> e <see cref="IAsignacionesDePuntoDeVenta"/>:
/// compila y corre sin las tablas de alcance; con sus implementaciones vacías, el alcance de todos es vacío.</item>
/// <item><b>Una lectura por petición</b>: es Scoped y memoriza la tarea; un comando que pregunte tres veces no consulta
/// tres veces.</item>
/// <item><b>En segundo plano</b> (<see cref="ContextoAmbiental"/>, lo que fija <c>IEjecutorEnCooperativa</c>) el alcance
/// es total: el proceso actúa sobre toda la cooperativa.</item>
/// </list>
/// </summary>
internal sealed class AlcanceDeInventarioDeLaPeticion(
    IHttpContextAccessor accessor,
    IActorActual actorActual,
    ICurrentUserPermissions permisos,
    IAsignacionesDeBodega bodegas,
    IAsignacionesDePuntoDeVenta puntos,
    ILogger<AlcanceDeInventarioDeLaPeticion> logger) : IAlcanceDeInventario
{
    public const string TodasLasBodegas = "Inventory.Scope.AllWarehouses";
    public const string TodosLosPuntos = "Inventory.Scope.AllPointsOfSale";

    private Task<AlcanceDeInventario>? _memo;

    public Task<AlcanceDeInventario> ObtenerAsync(CancellationToken ct = default) => _memo ??= ResolverAsync(ct);

    private async Task<AlcanceDeInventario> ResolverAsync(CancellationToken ct)
    {
        if (ContextoAmbiental.Activo) return AlcanceDeInventario.Total;
        if (accessor.HttpContext is null) return AlcanceDeInventario.Vacio;
        if (permisos.EsMaestroGlobal) return AlcanceDeInventario.Total;

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } userId)
        {
            logger.LogDebug("[Alcance.SinUsuario] La petición no tiene persona en SEC_Users; alcance de inventario vacío.");
            return AlcanceDeInventario.Vacio;
        }

        var concedidos = await permisos.ListAsync(ct);
        var todasLasBodegas = concedidos.Contains(TodasLasBodegas, StringComparer.OrdinalIgnoreCase);
        var todosLosPuntos = concedidos.Contains(TodosLosPuntos, StringComparer.OrdinalIgnoreCase);

        var deBodegas = await bodegas.BodegasDelUsuarioAsync(userId, ct);
        var dePuntos = await puntos.PuntosDelUsuarioAsync(userId, ct);
        return AlcanceDeInventario.De(todasLasBodegas, deBodegas, todosLosPuntos, dePuntos);
    }
}
