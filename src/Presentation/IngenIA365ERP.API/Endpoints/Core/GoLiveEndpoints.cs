using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.GoLive;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// La lista de go/no-go de una salida en vivo (feature 015, E-06, T0034; contracts/api.md §14; FR-227): leer con <c>Platform.GoLive.View</c>,
/// crear, verificar condiciones y decidir con <c>.Manage</c>, aprobar seguir con una condición en rojo con <c>.Override</c>. Las escrituras exigen
/// <c>Idempotency-Key</c>; cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class GoLiveEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Platform.GoLive.View";
    public const string PermisoDeAdministracion = "Platform.GoLive.Manage";
    public const string PermisoDeExcepcion = "Platform.GoLive.Override";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var listas = app.MapGroup("/api/core/go-live-checklists")
            .WithTags("Core Go/no-go")
            .RequireAuthorization()
            .AuditarIntentoDenegado(ModuloDeAuditoria.GoLive)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        listas.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new ListGoLiveChecklistsQuery(), ct))
            .WithName("Core_GoLive_List")
            .RequirePermission(PermisoDeConsulta);

        listas.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetGoLiveChecklistQuery(id), ct))
            .WithName("Core_GoLive_Get")
            .RequirePermission(PermisoDeConsulta);

        listas.MapPost("/", async (CrearListaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateGoLiveChecklistCommand(body.Name ?? string.Empty, body.Environment ?? string.Empty, body.TargetDate,
                    body.ManualItems?.Select(i => new CondicionManual(i.Module ?? string.Empty, i.Code ?? string.Empty, i.Description ?? string.Empty)).ToList() ?? [])
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/core/go-live-checklists/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Core_GoLive_Create")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);

        listas.MapPost("/{id:guid}/items/{itemId:guid}/check", async (Guid id, Guid itemId, VerificarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CheckGoLiveItemCommand(id, itemId, body.Status, body.Evidence, body.EvidenceAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_GoLive_CheckItem")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);

        listas.MapPost("/{id:guid}/items/{itemId:guid}/override", async (Guid id, Guid itemId, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new OverrideGoLiveItemCommand(id, itemId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_GoLive_OverrideItem")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeExcepcion);

        listas.MapPost("/{id:guid}/decision", async (Guid id, DecidirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DecideGoLiveCommand(id, body.Decision, body.Reason ?? string.Empty, body.ScenarioAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_GoLive_Decide")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);
    }

    public sealed record CondicionRequest(string? Module, string? Code, string? Description);

    public sealed record CrearListaRequest(string? Name, string? Environment, DateOnly TargetDate, IReadOnlyList<CondicionRequest>? ManualItems);

    public sealed record VerificarRequest(GoLiveItemStatus? Status, string? Evidence, Guid? EvidenceAttachmentPublicId);

    public sealed record MotivoRequest(string? Reason);

    public sealed record DecidirRequest(GoLiveChecklistStatus Decision, string? Reason, Guid? ScenarioAttachmentPublicId);
}
