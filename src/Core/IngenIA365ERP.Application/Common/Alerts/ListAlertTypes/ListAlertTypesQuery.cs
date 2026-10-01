using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Alerts.ListAlertTypes;

/// <summary>
/// Los tipos de alerta configurados en la cooperativa (feature 012, T39, T094; contracts/api.md §16.2,
/// <c>GET /api/inventory/alert-types</c>, <c>Inventory.Alerts.View</c>): por cada tipo del catálogo, la versión vigente
/// en <see cref="AsOf"/> (hoy si no viene) o, si no hay, la más reciente, con <c>activeRecipients</c> y
/// <c>withoutRecipient</c> (el reporte de completitud de SC-022). Los tipos sin ninguna versión no aparecen.
/// </summary>
public sealed record ListAlertTypesQuery(DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<AlertTypeDto>>>;

public sealed class ListAlertTypesQueryHandler(IApplicationDbContext db, IDestinatariosPorPermiso destinatarios, IDateTimeService reloj)
    : IRequestHandler<ListAlertTypesQuery, Result<IReadOnlyList<AlertTypeDto>>>
{
    public async Task<Result<IReadOnlyList<AlertTypeDto>>> Handle(ListAlertTypesQuery request, CancellationToken ct)
    {
        var fecha = request.AsOf ?? reloj.HoyLocal;
        var versiones = await db.AlertTypes.AsNoTracking().ToListAsync(ct);

        var resultado = new List<AlertTypeDto>();
        foreach (var definicion in TiposDeAlerta.Todos)
        {
            var delTipo = versiones.Where(v => v.TypeCode == definicion.TypeCode).OrderByDescending(v => v.ValidFrom).ToList();
            var version = delTipo.FirstOrDefault(v => v.VigenteEn(fecha)) ?? delTipo.FirstOrDefault();
            if (version is null) continue;

            var activos = definicion.UsaDestinatarios ? await destinatarios.ContarActivosAsync(version.Permisos(), ct) : 0;
            resultado.Add(ProyeccionDeAlertas.ADto(version, activos));
        }
        return Result.Success<IReadOnlyList<AlertTypeDto>>(resultado);
    }
}
