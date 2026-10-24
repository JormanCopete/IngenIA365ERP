using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Reversals;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>Las aplicaciones reversadas por los módulos (feature 015, US5, T0180; api.md §4). (nuevo)</summary>
public class ReversalsEndpoints : ICarterModule
{
    public const string Ver = "Receipts.Reversals.View";
    public const string Resolver = "Receipts.Reversals.Resolve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/module-reversals", "Recaudo Reversos de módulos");
        g.MapGet("/", async (ModuleReversalCause? state, ISender s, CancellationToken ct) => await s.Send(new ListModuleReversalsQuery(state), ct))
            .WithName("Receipts_ModuleReversals_List").RequirePermission(new AlgunPermiso(Ver, Resolver));
        g.MapPost("/{id:guid}/confirm-cause", async (Guid id, CausaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ConfirmModuleReversalCauseCommand(id, body.Cause, body.Note ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ModuleReversals_ConfirmCause").ConClaveDeOperacion().RequirePermission(Resolver);
    }

    public sealed record CausaRequest(ModuleReversalCause Cause, string? Note);
}
