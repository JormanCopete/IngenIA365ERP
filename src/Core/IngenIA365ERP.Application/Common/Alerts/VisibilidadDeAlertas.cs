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
///
/// <para>
/// Feature 013 (T0303, C68, SC-015): una alerta de Cartera guarda en <c>COR_Alerts</c> la oficina (<c>ScopeBranchPublicId</c>, la
/// <c>COR_Branches</c> del caso) y la línea (<c>ScopeCreditLinePublicId</c>); por permiso destinatario sólo la ve quien además las tiene en
/// su alcance de Cartera (<see cref="IAlcanceDeCartera"/>, que falla cerrado). La que se le notificó (p. ej. la enrutada a
/// <c>CompanyAdmin</c> sin destinatario) la ve siempre.
/// </para>
/// </summary>
public sealed class VisibilidadDeAlertas(
    IApplicationDbContext db,
    ICurrentUserPermissions permisos,
    IActorActual actorActual,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IAsignacionesDeBodega bodegas,
    IAsignacionesDePuntoDeVenta puntos,
    IAlcanceDeCartera? alcanceDeCartera = null)
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

        // Feature 013 (T0303, C68): la oficina y la línea de una alerta de Cartera, contra el alcance de Cartera (sin él, cerrado).
        var cartera = alcanceDeCartera is null ? AlcanceDeCartera.Vacio : await alcanceDeCartera.ObtenerAsync(ct);
        var todasLasOficinas = cartera.TodasLasOficinas;
        var todasLasLineas = cartera.TodasLasLineas;
        var idsDeOficina = cartera.Oficinas.ToArray();
        var idsDeLinea = cartera.Lineas.ToArray();
        Guid[] oficinasPermitidas = todasLasOficinas ? [] : await db.Branches.AsNoTracking()
            .Where(b => idsDeOficina.Contains(b.Id)).Select(b => b.PublicId).ToArrayAsync(ct);
        Guid[] lineasPermitidas = todasLasLineas ? [] : await db.CreditLines.AsNoTracking()
            .Where(l => idsDeLinea.Contains(l.Id)).Select(l => l.PublicId).ToArrayAsync(ct);

        return db.Alerts.Where(a =>
            (tiposPermitidos.Contains(a.AlertTypeId)
             && (a.ScopeWarehousePublicId == null || todasLasBodegas || bodegasPermitidas.Contains(a.ScopeWarehousePublicId.Value))
             && (a.ScopePointOfSalePublicId == null || todosLosPuntos || puntosPermitidos.Contains(a.ScopePointOfSalePublicId.Value))
             && (a.ScopeBranchPublicId == null || todasLasOficinas || oficinasPermitidas.Contains(a.ScopeBranchPublicId.Value))
             && (a.ScopeCreditLinePublicId == null || todasLasLineas || lineasPermitidas.Contains(a.ScopeCreditLinePublicId.Value)))
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
