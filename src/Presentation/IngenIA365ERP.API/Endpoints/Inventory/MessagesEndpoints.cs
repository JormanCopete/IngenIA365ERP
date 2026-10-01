using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// La bandeja de mensajes de Inventario (feature 012, I2, T529; contracts/api.md §25.2): la lista con sus contadores por
/// estado y la vista previa del envío posterior (<c>closure=true</c>), el detalle con contenido, intentos y dependencias, el
/// reproceso de rechazados y el envío posterior de «no aplica». Las dos órdenes llevan <c>Idempotency-Key</c>, motivo y
/// responden <b>202</b>: crean un lote que corre el despachador en segundo plano, con la persona que lo ordenó como actor
/// (FR-083). Todo con <c>RequireAuthorization</c>, el sobre de errores y su permiso; sin él, el 404 genérico. (nuevo)
/// </summary>
public class MessagesEndpoints : ICarterModule
{
    public const string Ver = "Inventory.Messages.View";
    public const string Reprocesar = "Inventory.Messages.Reprocess";
    public const string EnviarNoAplica = "Inventory.Messages.SendNotApplicable";

    public sealed record ReprocesoRequest(IReadOnlyList<Guid>? MessagePublicIds, string? Reason, string? Destination);

    public sealed record EnvioPosteriorRequest(
        DateOnly From,
        DateOnly To,
        IReadOnlyList<Guid>? DocumentTypePublicIds,
        Guid CutoffMessagePublicId,
        string? Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/inventory/messages")
            .WithTags("Inventory Messages")
            .RequireAuthorization();

        g.MapGet("/", async (DeliveryStatus? status, string? destination, string? type, Guid? document, string? documentNumber, string? documentType,
                    Guid? batch, DateOnly? from, DateOnly? to, bool? closure, PrevalidationOutcome? prevalidationOutcome, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListIntegrationMessagesQuery(status, destination, type, document, documentNumber, documentType, batch, from, to,
                    closure ?? false, prevalidationOutcome, page ?? 1, pageSize ?? 50), ct))
            .WithName("Inventory_Messages_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapGet("/{id:guid}", async (Guid id, string? destination, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetIntegrationMessageQuery(id, destination), ct))
            .WithName("Inventory_Messages_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/reprocess", async (ReprocesoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new ReprocessMessagesCommand(body.MessagePublicIds ?? [], body.Reason ?? string.Empty,
                    string.IsNullOrWhiteSpace(body.Destination) ? IntegrationDestinations.Accounting : body.Destination)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess
                    ? (object)Results.Accepted($"/api/accounting/inventory/batches/{r.Value.BatchPublicId}", new
                    {
                        batchPublicId = r.Value.BatchPublicId, number = r.Value.Number, trigger = r.Value.Trigger,
                        messages = r.Value.Messages, dragged = r.Value.Dragged,
                    })
                    : r;
            })
            .WithName("Inventory_Messages_Reprocess").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Reprocesar);

        g.MapPost("/send-not-applicable", async (EnvioPosteriorRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new SendNotApplicableMessagesCommand(body.From, body.To, body.DocumentTypePublicIds, body.CutoffMessagePublicId,
                    body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess
                    ? (object)Results.Accepted($"/api/accounting/inventory/batches/{r.Value.BatchPublicId}", new
                    {
                        batchPublicId = r.Value.BatchPublicId, number = r.Value.Number, trigger = r.Value.Trigger,
                        documents = r.Value.Documents, messages = r.Value.Messages,
                    })
                    : r;
            })
            .WithName("Inventory_Messages_SendNotApplicable").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(EnviarNoAplica);
    }
}
