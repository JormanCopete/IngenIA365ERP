using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Alerts.GetAlert;

/// <summary>
/// Una alerta (feature 012, T39, T094; contracts/api.md §16.1, <c>GET /api/inventory/alerts/{id}</c>,
/// <c>Inventory.Alerts.View</c>). Si no alcanza a quien pregunta (permiso destinatario y
/// <see cref="IAlcanceDeInventario"/>, o notificada a él), el mismo 404 <c>Alerts.Alert.NotFound</c> que una
/// inexistente.
/// </summary>
public sealed record GetAlertQuery(Guid AlertPublicId) : IRequest<Result<AlertDto>>;

public sealed class GetAlertQueryHandler(VisibilidadDeAlertas visibilidad) : IRequestHandler<GetAlertQuery, Result<AlertDto>>
{
    public async Task<Result<AlertDto>> Handle(GetAlertQuery request, CancellationToken ct)
    {
        var alerta = await (await visibilidad.VisiblesAsync(ct)).AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == request.AlertPublicId, ct);
        return alerta is null
            ? Result.Failure<AlertDto>(ErroresDeAlertas.AlertaInexistente())
            : Result.Success(ProyeccionDeAlertas.ADto(alerta));
    }
}
