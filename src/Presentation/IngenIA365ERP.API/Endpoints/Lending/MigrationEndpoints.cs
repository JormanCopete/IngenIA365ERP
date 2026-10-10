using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Migration;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las cargas de migración de Cartera (feature 013, T0234, C107; contracts/api.md §19.1), en su forma mínima: listar, ver, crear el
/// borrador con su corte y aplicar una carga sin partes, todo con <c>Lending.Migration.Load</c>. Las escrituras exigen
/// <c>Idempotency-Key</c>; una carga que no existe responde el 404 genérico (<c>Lending.MigrationLoad.NotFound</c>). Desde US17 (T0312) va sobre
/// <see cref="LendingGroup"/> (auditoría de Cartera); US8 suma las partes, plantillas, cuadre, conciliación y diferencias de §19.1–§19.2.
/// Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class MigrationEndpoints : ICarterModule
{
    public const string Permiso = "Lending.Migration.Load";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // US17 (T0312): sobre el grupo base de Cartera, que suma la auditoría del intento denegado y del rechazo de forma.
        var cargas = LendingGroup.Grupo(app, "/migration/loads", "Cartera Migración");

        cargas.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new ListMigrationLoadsQuery(), ct))
            .WithName("Lending_MigrationLoads_List")
            .RequirePermission(Permiso);

        cargas.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetMigrationLoadQuery(id), ct))
            .WithName("Lending_MigrationLoads_Get")
            .RequirePermission(Permiso);

        cargas.MapPost("/", async (CrearCargaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateMigrationLoadCommand(body.CutoffDate, body.Notes) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/lending/migration/loads/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Lending_MigrationLoads_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);

        cargas.MapPost("/{id:guid}/apply", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyMigrationLoadCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_MigrationLoads_Apply")
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);
    }

    /// <summary>El cuerpo del alta (§19.1, <c>{ cutoffDate, notes? }</c>).</summary>
    public sealed record CrearCargaRequest(DateOnly CutoffDate, string? Notes);

    /// <summary>El cuerpo de aplicar (§19.1, <c>{ reason }</c>).</summary>
    public sealed record MotivoRequest(string? Reason);
}
