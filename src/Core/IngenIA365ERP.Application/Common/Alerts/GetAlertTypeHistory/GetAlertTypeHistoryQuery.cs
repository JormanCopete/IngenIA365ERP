using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Alerts.GetAlertTypeHistory;

/// <summary>
/// Las vigencias de un tipo de alerta, la más reciente primero (feature 012, T39, T094; contracts/api.md §16.2,
/// <c>GET /api/inventory/alert-types/{typeCode}/history</c>, <c>Inventory.Alerts.View</c>). Un tipo fuera del catálogo
/// es 404 <c>Alerts.Type.NotFound</c>; uno del catálogo sin versiones, una lista vacía.
/// </summary>
public sealed record GetAlertTypeHistoryQuery(string TypeCode) : IRequest<Result<IReadOnlyList<AlertTypeDto>>>;

public sealed class GetAlertTypeHistoryQueryHandler(IApplicationDbContext db, IDestinatariosPorPermiso destinatarios)
    : IRequestHandler<GetAlertTypeHistoryQuery, Result<IReadOnlyList<AlertTypeDto>>>
{
    public async Task<Result<IReadOnlyList<AlertTypeDto>>> Handle(GetAlertTypeHistoryQuery request, CancellationToken ct)
    {
        var definicion = TiposDeAlerta.Buscar(request.TypeCode);
        if (definicion is null) return Result.Failure<IReadOnlyList<AlertTypeDto>>(ErroresDeAlertas.TipoInexistente(request.TypeCode));

        var versiones = await db.AlertTypes.AsNoTracking()
            .Where(t => t.TypeCode == request.TypeCode)
            .OrderByDescending(t => t.ValidFrom)
            .ToListAsync(ct);

        var resultado = new List<AlertTypeDto>(versiones.Count);
        foreach (var version in versiones)
        {
            var activos = definicion.UsaDestinatarios ? await destinatarios.ContarActivosAsync(version.Permisos(), ct) : 0;
            resultado.Add(ProyeccionDeAlertas.ADto(version, activos));
        }
        return Result.Success<IReadOnlyList<AlertTypeDto>>(resultado);
    }
}
