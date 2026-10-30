using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Checks;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// El canje completo de los cheques recibidos (feature 015, R2, fase 37, T0428; FR-084): armar el lote de canje de la remesa del día y cruzar el abono del extracto que paga sus
/// cheques. Las rutas son (nuevo), DT §2.7; la devolución sigue en <c>/api/receipts/received-checks/{id}/return</c>. (nuevo)
/// </summary>
public class ClearingBatchesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/clearing-batches", "Recaudo Canje de cheques");
        g.MapGet("/", async (ClearingBatchState? state, ISender s, CancellationToken ct) => await s.Send(new ListClearingBatchesQuery(state), ct))
            .WithName("Receipts_ClearingBatches_List").RequirePermission(new AlgunPermiso(ReceivedChecksEndpoints.Ver, ReceivedChecksEndpoints.Gestionar));
        g.MapPost("/", async (LoteRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(new CreateClearingBatchCommand(body.OwnBankAccountPublicId ?? Guid.Empty, body.CheckPublicIds ?? [], body.RemittancePublicId,
                    body.DepositedOn) { OperationKey = http.ClaveDeOperacion() }, ct), l => $"/api/receipts/clearing-batches/{l.PublicId}"))
            .WithName("Receipts_ClearingBatches_Create").ConClaveDeOperacion().RequirePermission(ReceivedChecksEndpoints.Gestionar);
        g.MapPost("/{id:guid}/statement-match", async (Guid id, CruceRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MatchClearingBatchStatementCommand(id, body.StatementLinePublicId ?? Guid.Empty, body.CheckPublicIds) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ClearingBatches_StatementMatch").ConClaveDeOperacion().RequirePermission(ReceivedChecksEndpoints.Gestionar);
    }

    public sealed record LoteRequest(Guid? OwnBankAccountPublicId, IReadOnlyList<Guid>? CheckPublicIds, Guid? RemittancePublicId, DateOnly? DepositedOn);

    public sealed record CruceRequest(Guid? StatementLinePublicId, IReadOnlyList<Guid>? CheckPublicIds);
}
