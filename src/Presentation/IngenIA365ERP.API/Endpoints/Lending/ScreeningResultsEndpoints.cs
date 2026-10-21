using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Origination;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El resultado de las listas restrictivas y su liberación (feature 013, US2, T0408; contracts/api.md §4.5, T70): la lista, el detalle con la
/// evidencia y liberar con motivo. Todo con <c>Compliance.RestrictiveLists.Release</c> (el oficial de cumplimiento; C84), también la lectura;
/// quien radicó o pidió el cupo no libera (<c>Lending.Segregation.Conflict</c>). Liberar lleva <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class ScreeningResultsEndpoints : ICarterModule
{
    public const string Liberar = "Compliance.RestrictiveLists.Release";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var listas = LendingGroup.Grupo(app, "/screening-results", "Cartera Listas restrictivas");

        listas.MapGet("/", async (string? status, Guid? personPublicId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListScreeningResultsQuery(status, personPublicId, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_ScreeningResults_List")
            .RequirePermission(Liberar);

        listas.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetScreeningResultQuery(id), ct))
            .WithName("Lending_ScreeningResults_Get")
            .RequirePermission(Liberar);

        listas.MapPost("/{id:guid}/release", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReleaseScreeningMatchCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_ScreeningResults_Release")
            .ConClaveDeOperacion()
            .RequirePermission(Liberar);
    }

    /// <summary>El motivo de la liberación.</summary>
    public sealed record MotivoRequest(string? Reason);
}
