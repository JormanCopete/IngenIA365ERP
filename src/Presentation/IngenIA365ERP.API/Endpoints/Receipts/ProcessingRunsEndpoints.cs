using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>Las corridas no interactivas y el reproceso de una línea con su misma clave (feature 015, US6, T0198; contracts/api.md §5). (nuevo)</summary>
public class ProcessingRunsEndpoints : ICarterModule
{
    public const string Identificar = BankItemsEndpoints.Identificar;
    public const string AplicarGiro = "Receipts.Paymasters.ApplyRemittance";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/processing-runs", "Recaudo Corridas");
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetProcessingRunQuery(id), ct))
            .WithName("Receipts_ProcessingRuns_Get").RequirePermission(new AlgunPermiso(Identificar, AplicarGiro));
        g.MapPost("/{id:guid}/lines/{lineId:guid}/retry", async (Guid id, Guid lineId, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RetryProcessingRunLineCommand(id, lineId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ProcessingRuns_RetryLine").ConClaveDeOperacion().RequirePermission(new AlgunPermiso(Identificar, AplicarGiro));
    }
}
