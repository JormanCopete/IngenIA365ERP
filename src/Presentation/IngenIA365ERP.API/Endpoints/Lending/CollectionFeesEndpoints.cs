using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections.Fees;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los gastos de cobranza informados (feature 013, K4, US12, T0956; FR-121, T77): los pendientes de causar (<c>Lending.Collections.Read</c>) y causarlos
/// por persona, sólo los que ya tienen su aviso <c>Sent</c> o <c>Delivered</c> (<c>Lending.CollectionStages.Manage</c>, con <c>Idempotency-Key</c>).
/// La ruta es de Cartera (<c>/api/lending/collection-fees</c>) y no está en contracts/api.md §14; se agrega allí. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class CollectionFeesEndpoints : ICarterModule
{
    public const string Leer = "Lending.Collections.Read";
    public const string Causar = "Lending.CollectionStages.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var gastos = LendingGroup.Grupo(app, "/collection-fees", "Cartera Gastos de cobranza");

        gastos.MapGet("/pending", async (Guid? personPublicId, Guid? loanPublicId, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPendingCollectionFeesQuery(personPublicId, loanPublicId), ct))
            .WithName("Lending_CollectionFees_Pending")
            .RequirePermission(Leer);

        gastos.MapPost("/cause", async (CausarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CauseCollectionFeesCommand(body.PersonPublicId, body.NoticePublicIds) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CollectionFees_Cause")
            .ConClaveDeOperacion()
            .RequirePermission(Causar);
    }

    /// <summary>Causar los gastos informados de una persona (todos, o los avisos que se indiquen).</summary>
    public sealed record CausarRequest(Guid PersonPublicId, IReadOnlyList<Guid>? NoticePublicIds);
}
