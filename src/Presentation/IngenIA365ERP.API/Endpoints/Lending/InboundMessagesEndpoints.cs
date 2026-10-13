using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los recibos de lo que llega a Cartera (feature 013, US6, T0597; contracts/api.md §16.2; T17, C95, C102): las ventas a crédito y sus ajustes de
/// Comercial y los resultados del giro de Tesorería, con sus evaluaciones y la respuesta enviada (<c>Lending.Messages.Read</c>, con el alcance de la
/// oficina del crédito), y marcar no reprocesable un recibo rechazado (<c>Lending.Messages.Reprocess</c>, con motivo e <c>Idempotency-Key</c>), que
/// emite en la misma transacción la <c>ObligacionRechazada</c> definitiva. Fuera del alcance, el mismo 404 que si el recibo no existiera. El consumo no
/// tiene ruta: lo hace el despachador. (nuevo)
/// </summary>
public class InboundMessagesEndpoints : ICarterModule
{
    public const string LeerMensajes = "Lending.Messages.Read";
    public const string ReprocesarMensajes = "Lending.Messages.Reprocess";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var recibos = LendingGroup.Grupo(app, "/inbound-messages", "Cartera Mensajes recibidos");

        recibos.MapGet("/", async (InboundMessageStatus? status, string? type, DateOnly? from, DateOnly? to, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListLendingInboundMessagesQuery(status, type, from, to, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_InboundMessages_List")
            .RequirePermission(LeerMensajes);

        recibos.MapGet("/{messagePublicId:guid}", async (Guid messagePublicId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLendingInboundMessageQuery(messagePublicId), ct))
            .WithName("Lending_InboundMessages_Get")
            .RequirePermission(LeerMensajes);

        recibos.MapPost("/{messagePublicId:guid}/mark-not-retryable", async (Guid messagePublicId, MotivoRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new MarkLendingInboundMessageNotRetryableCommand(messagePublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_InboundMessages_MarkNotRetryable")
            .ConClaveDeOperacion()
            .RequirePermission(ReprocesarMensajes);
    }

    /// <summary>El motivo de marcar un recibo no reprocesable.</summary>
    public sealed record MotivoRequest(string? Reason);
}
