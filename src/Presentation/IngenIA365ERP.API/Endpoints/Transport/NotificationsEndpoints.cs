using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Application.Transport.Notifications;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// El tablero de envíos a sistemas externos (feature 020, US9; R18; contracts/api.md §3): la lista con conteos por estado, el detalle con sus
/// intentos (código, extracto, duración y cuerpo saneado), reintentar (con <c>Idempotency-Key</c>) y cerrar a mano con motivo.
/// </summary>
public sealed class NotificationsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TransportGroup.Grupo(app, "/notifications", "Transporte Envíos");

        g.MapGet("/", async (string? status, DateOnly? from, DateOnly? to, Guid? vehicle, Guid? service, ISender s, CancellationToken ct) =>
                await s.Send(new ListConnectorRequestsQuery(status, from, to, vehicle, service), ct))
            .WithName("Transport_Notifications_List").RequirePermission(TransportPermissions.NotificationsView);

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetConnectorRequestQuery(id), ct))
            .WithName("Transport_Notifications_Get").RequirePermission(TransportPermissions.NotificationsView);

        g.MapPost("/{id:guid}/retry", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            {
                var r = await s.Send(new RetryConnectorRequestCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? Results.NoContent() : (object)r;
            })
            .WithName("Transport_Notifications_Retry").ConClaveDeOperacion().RequirePermission(TransportPermissions.NotificationsManage);

        g.MapPost("/{id:guid}/resolve", async (Guid id, MotivoRequest body, ISender s, CancellationToken ct) =>
            {
                var r = await s.Send(new ResolveConnectorRequestCommand(id, body.Reason ?? string.Empty), ct);
                return r.IsSuccess ? Results.NoContent() : (object)r;
            })
            .WithName("Transport_Notifications_Resolve").RequirePermission(TransportPermissions.NotificationsManage);
    }

    public sealed record MotivoRequest(string? Reason);
}
