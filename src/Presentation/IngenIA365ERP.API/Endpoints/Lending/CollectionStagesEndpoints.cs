using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections.Stages;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las etapas de cobranza versionadas (feature 013, K4, US12, T0956; contracts/api.md §14.5, FR-117): la versión vigente a una fecha
/// (<c>Lending.Collections.Read</c>) y una versión nueva (<c>Lending.CollectionStages.Manage</c>, con <c>Idempotency-Key</c> y motivo). Fuera de
/// permiso, el mismo 404 que si la ruta no existiera (§1.3). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class CollectionStagesEndpoints : ICarterModule
{
    public const string Leer = "Lending.Collections.Read";
    public const string Administrar = "Lending.CollectionStages.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var etapas = LendingGroup.Grupo(app, "/collection-stages", "Cartera Etapas de cobranza");

        etapas.MapGet("/", async (DateOnly? date, ISender sender, CancellationToken ct) => await sender.Send(new ListCollectionStagesQuery(date), ct))
            .WithName("Lending_CollectionStages_List")
            .RequirePermission(Leer);

        etapas.MapPost("/versions", async (VersionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddCollectionStageVersionCommand(body.Stages ?? [], body.ValidFrom, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), "/api/lending/collection-stages"))
            .WithName("Lending_CollectionStages_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo de una versión nueva de etapas (§14.5).</summary>
    public sealed record VersionRequest(IReadOnlyList<CollectionStageInput>? Stages, DateOnly ValidFrom, string? Reason);
}
