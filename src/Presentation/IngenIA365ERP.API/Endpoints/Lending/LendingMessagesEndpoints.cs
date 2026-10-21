using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration.Queries;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// La bandeja de mensajes de Cartera (feature 013, US7; contracts/api.md §16.1; T19, CH32): la misma bandeja de la plataforma
/// (<see cref="ListIntegrationMessagesQuery"/>, <see cref="GetIntegrationMessageQuery"/>, <see cref="ReprocessMessagesCommand"/>,
/// <see cref="SendNotApplicableMessagesCommand"/>) con la vista de Cartera (<c>Modulo = CAR</c>: sus mensajes y los que le llegan, por el
/// alcance de oficinas). <c>?origin=</c> filtra por la operación, la parte o el cierre de origen: es a donde lleva el enlace de un comprobante
/// de Cartera (<c>EnlacesDeOrigen</c>). El reproceso sólo toma mensajes que esta vista deja ver; las dos órdenes llevan motivo e
/// <c>Idempotency-Key</c> y responden <b>202</b>. (nuevo)
/// </summary>
public class LendingMessagesEndpoints : ICarterModule
{
    public const string LeerMensajes = "Lending.Messages.Read";
    public const string ReprocesarMensajes = "Lending.Messages.Reprocess";
    public const string EnviarNoAplica = "Lending.Messages.SendNotApplicable";

    public sealed record ReprocesoRequest(IReadOnlyList<Guid>? MessagePublicIds, string? Reason, string? Destination);

    /// <summary>El envío posterior: rango, tipos de hecho (<c>LND_FactTypes</c> por <c>PublicId</c>), el corte de la vista previa y motivo.</summary>
    public sealed record EnvioPosteriorRequest(
        DateOnly From,
        DateOnly To,
        IReadOnlyList<Guid>? Types,
        Guid CutoffMessagePublicId,
        string? Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = LendingGroup.Grupo(app, "/messages", "Cartera Bandeja de mensajes");

        g.MapGet("/", async (DeliveryStatus? status, string? destination, string? type, Guid? origin, string? originNumber, Guid? batch, DateOnly? from,
                DateOnly? to, bool? closure, PrevalidationOutcome? prevalidationOutcome, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListIntegrationMessagesQuery(status, destination, type, origin, originNumber, null, batch, from, to, closure ?? false,
                    prevalidationOutcome, page ?? 1, pageSize ?? 50, ModulosDeOrigen.Cartera), ct))
            .WithName("Lending_Messages_List")
            .RequirePermission(LeerMensajes);

        g.MapGet("/{id:guid}", async (Guid id, string? destination, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetIntegrationMessageQuery(id, destination, ModulosDeOrigen.Cartera), ct))
            .WithName("Lending_Messages_Get")
            .RequirePermission(LeerMensajes);

        g.MapPost("/reprocess", async (ReprocesoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var destino = string.IsNullOrWhiteSpace(body.Destination) ? IntegrationDestinations.Accounting : body.Destination;
                // Fuera de la vista de Cartera, lo mismo que inexistente: el reproceso de la plataforma no conoce módulos.
                foreach (var id in (body.MessagePublicIds ?? []).Distinct())
                {
                    var visible = await sender.Send(new GetIntegrationMessageQuery(id, destino, ModulosDeOrigen.Cartera), ct);
                    if (visible.IsFailure) return (object)visible;
                }
                var r = await sender.Send(new ReprocessMessagesCommand(body.MessagePublicIds ?? [], body.Reason ?? string.Empty, destino)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess
                    ? Results.Accepted($"/api/accounting/lending/batches/{r.Value.BatchPublicId}", new
                    {
                        batchPublicId = r.Value.BatchPublicId, number = r.Value.Number, trigger = r.Value.Trigger,
                        messages = r.Value.Messages, dragged = r.Value.Dragged,
                    })
                    : r;
            })
            .WithName("Lending_Messages_Reprocess")
            .ConClaveDeOperacion()
            .RequirePermission(ReprocesarMensajes);

        g.MapPost("/send-not-applicable", async (EnvioPosteriorRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new SendNotApplicableMessagesCommand(body.From, body.To, body.Types, body.CutoffMessagePublicId,
                    body.Reason ?? string.Empty, ModulosDeOrigen.Cartera) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess
                    ? (object)Results.Accepted($"/api/accounting/lending/batches/{r.Value.BatchPublicId}", new
                    {
                        batchPublicId = r.Value.BatchPublicId, number = r.Value.Number, trigger = r.Value.Trigger,
                        documents = r.Value.Documents, messages = r.Value.Messages,
                    })
                    : r;
            })
            .WithName("Lending_Messages_SendNotApplicable")
            .ConClaveDeOperacion()
            .RequirePermission(EnviarNoAplica);
    }
}
