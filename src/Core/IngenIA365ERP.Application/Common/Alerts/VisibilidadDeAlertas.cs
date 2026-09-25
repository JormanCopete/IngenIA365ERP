using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Entities.Alerts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// Qué alertas alcanzan a quien pregunta (feature 012, T39, T094; contracts/api.md §16.1). Una alerta lo alcanza si
/// (a) tiene uno de los permisos destinatarios de la versión del tipo con que se levantó y alcance sobre su bodega y su
/// punto (<see cref="IAlcanceDeInventario"/>), o (b) se la notificaron (así le llegan las que se enrutaron a
/// <c>CompanyAdmin</c> y las de <c>Aprobaciones.Pendiente</c>, cuyos destinatarios son el permiso del nivel). Las demás
/// son el mismo 404 que una inexistente. El administrador maestro las ve todas. (nuevo)
/// </summary>
public sealed class VisibilidadDeAlertas(
    IApplicationDbContext db,
    ICurrentUserPermissions permisos,
    IActorActual actorActual,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IAsignacionesDeBodega bodegas,
    IAsignacionesDePuntoDeVenta puntos)
{
    /// <summary>Las alertas visibles, como consulta (sin materializar) para que quien llama filtre y pagine.</summary>
    public async Task<IQueryable<Alert>> VisiblesAsync(CancellationToken ct)
    {
        if (permisos.EsMaestroGlobal) return db.Alerts;

        var concedidos = new HashSet<string>(await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var actor = await actorActual.ObtenerAsync(ct);
        var yo = actor.UserPublicId ?? Guid.Empty;

        var tipos = await db.AlertTypes.IgnoreQueryFilters().AsNoTracking()
            .Select(t => new { t.Id, t.RecipientPermissions })
            .ToListAsync(ct);
        var tiposPermitidos = tipos
            .Where(t => new AlertType { RecipientPermissions = t.RecipientPermissions }.Permisos().Any(concedidos.Contains))
            .Select(t => t.Id)
            .ToArray();

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var todasLasBodegas = alcance.TodasLasBodegas;
        var todosLosPuntos = alcance.TodosLosPuntos;
        Guid[] bodegasPermitidas = todasLasBodegas ? [] : await PermitidasAsync(
            db.Alerts.Where(a => a.ScopeWarehousePublicId != null).Select(a => a.ScopeWarehousePublicId!.Value),
            bodegas.BuscarAsync, alcance.IncluyeBodega, ct);
        Guid[] puntosPermitidos = todosLosPuntos ? [] : await PermitidasAsync(
            db.Alerts.Where(a => a.ScopePointOfSalePublicId != null).Select(a => a.ScopePointOfSalePublicId!.Value),
            puntos.BuscarAsync, alcance.IncluyePunto, ct);

        return db.Alerts.Where(a =>
            (tiposPermitidos.Contains(a.AlertTypeId)
             && (a.ScopeWarehousePublicId == null || todasLasBodegas || bodegasPermitidas.Contains(a.ScopeWarehousePublicId.Value))
             && (a.ScopePointOfSalePublicId == null || todosLosPuntos || puntosPermitidos.Contains(a.ScopePointOfSalePublicId.Value)))
            || db.Notifications.Any(n => n.AlertPublicId == a.PublicId && n.RecipientUserPublicId == yo));
    }

    /// <summary>De los <c>PublicId</c> que aparecen en las alertas, los que el alcance de la petición incluye.</summary>
    private static async Task<Guid[]> PermitidasAsync(
        IQueryable<Guid> enAlertas,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>>> buscar,
        Func<int, bool> incluye,
        CancellationToken ct)
    {
        var distintas = await enAlertas.Distinct().ToListAsync(ct);
        if (distintas.Count == 0) return [];
        var encontradas = await buscar(distintas, ct);
        return encontradas.Values.Where(e => incluye(e.Id)).Select(e => e.PublicId).ToArray();
    }
}
