using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Las solicitudes de tasa negociada (feature 014, US6, T178; contracts/api.md §6 de la 014; FR-127), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/negotiated-rate-requests</c> —lista y detalle para quien pide o decide, aprobar y negar (<c>Lending.NegotiatedRates.Approve</c>, con el nivel
/// que la matriz pide y la segregación por persona: quien pidió no decide) y retirar (<c>.Request</c>, sólo quien la pidió)—. Las reglas viven en
/// <c>/api/lending/lines/{id}/negotiated-rate-rules</c> (<see cref="LinesEndpoints"/>). Toda escritura con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class NegotiatedRatesEndpoints : ICarterModule
{
    public const string Pedir = "Lending.NegotiatedRates.Request";
    public const string Aprobar = "Lending.NegotiatedRates.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var solicitudes = LendingGroup.Grupo(app, "/negotiated-rate-requests", "Cartera Tasas negociadas");

        solicitudes.MapGet("/", async (NegotiatedRateRequestStatus? status, Guid? line, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListNegotiatedRateRequestsQuery(status, line, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_NegotiatedRateRequests_List")
            .RequirePermission(new AlgunPermiso(Pedir, Aprobar));

        solicitudes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetNegotiatedRateRequestQuery(id), ct))
            .WithName("Lending_NegotiatedRateRequests_Get")
            .RequirePermission(new AlgunPermiso(Pedir, Aprobar));

        solicitudes.MapPost("/{id:guid}/approve", async (Guid id, DecidirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DecideNegotiatedRateCommand(id, NegotiatedRateDecision.Approve, body.Reason ?? string.Empty, body.ApprovalPublicId,
                    body.SessionItemPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_NegotiatedRateRequests_Approve")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        solicitudes.MapPost("/{id:guid}/reject", async (Guid id, DecidirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DecideNegotiatedRateCommand(id, NegotiatedRateDecision.Reject, body.Reason ?? string.Empty, null, body.SessionItemPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_NegotiatedRateRequests_Reject")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        solicitudes.MapPost("/{id:guid}/withdraw", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new WithdrawNegotiatedRateRequestCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_NegotiatedRateRequests_Withdraw")
            .ConClaveDeOperacion()
            .RequirePermission(Pedir);
    }

    /// <summary>El cuerpo de la decisión: el motivo, la aprobación del aprobador presente y el punto de acta si decide un órgano.</summary>
    public sealed record DecidirRequest(string? Reason, Guid? ApprovalPublicId, Guid? SessionItemPublicId);

    /// <summary>El cuerpo del retiro: el motivo.</summary>
    public sealed record MotivoRequest(string? Reason);
}
