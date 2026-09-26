using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Enums.Alerts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Alerts.ListAlerts;

/// <summary>
/// La bandeja de alertas (feature 012, T39, T094; contracts/api.md §16.1,
/// <c>GET /api/inventory/alerts?status=&amp;typeCode=&amp;module=&amp;severity=&amp;from=&amp;to=&amp;page=&amp;pageSize=</c>,
/// <c>Inventory.Alerts.View</c>): las que alcanzan a quien pregunta (permiso destinatario y alcance de bodega y punto
/// por <see cref="IAlcanceDeInventario"/>, o notificadas a él; <see cref="VisibilidadDeAlertas"/>), la más reciente
/// primero. El contador de la campana es <c>totalCount</c> con <c>status=Pending&amp;pageSize=1</c>. Las fechas filtran
/// por el día (UTC) en que se levantó.
/// </summary>
public sealed record ListAlertsQuery(
    AlertStatus? Status = null,
    string? TypeCode = null,
    string? Module = null,
    AlertSeverity? Severity = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<AlertDto>>>;

public sealed class ListAlertsQueryHandler(VisibilidadDeAlertas visibilidad)
    : IRequestHandler<ListAlertsQuery, Result<PagedResult<AlertDto>>>
{
    public async Task<Result<PagedResult<AlertDto>>> Handle(ListAlertsQuery request, CancellationToken ct)
    {
        var consulta = (await visibilidad.VisiblesAsync(ct)).AsNoTracking();
        if (request.Status is { } estado) consulta = consulta.Where(a => a.Status == estado);
        if (!string.IsNullOrWhiteSpace(request.TypeCode)) consulta = consulta.Where(a => a.TypeCode == request.TypeCode);
        if (!string.IsNullOrWhiteSpace(request.Module)) consulta = consulta.Where(a => a.Module == request.Module);
        if (request.Severity is { } severidad) consulta = consulta.Where(a => a.Severity == severidad);
        if (request.From is { } desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(a => a.RaisedAt >= inicio);
        }
        if (request.To is { } hasta)
        {
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(a => a.RaisedAt < fin);
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta
            .OrderByDescending(a => a.RaisedAt).ThenByDescending(a => a.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize)
            .Take(pagina.SafePageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<AlertDto>(
            filas.Select(ProyeccionDeAlertas.ADto).ToList(), pagina.SafePage, pagina.SafePageSize, total));
    }
}
