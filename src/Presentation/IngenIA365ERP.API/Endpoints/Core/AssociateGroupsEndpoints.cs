using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.AssociateGroups;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// Grupos del asociado (<c>/api/core/associate-groups</c>): la clasificación libre de la cooperativa (zona, empresa,
/// gremio, agencia…), independiente de la pagaduría y de la contabilidad. Leer con <c>Core.AssociateGroups.View</c>;
/// escribir con <c>Core.AssociateGroups.Manage</c> e <c>Idempotency-Key</c>. Inactivar y reactivar llevan motivo
/// (<c>POST /{id}/deactivate|reactivate</c>). Cada ruta sólo reenvía al <see cref="ISender"/>.
/// </summary>
public class AssociateGroupsEndpoints : ICarterModule
{
    public const string Ver = "Core.AssociateGroups.View";
    public const string Administrar = "Core.AssociateGroups.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/core/associate-groups").WithTags("Core Associate Groups").RequireAuthorization();

        g.MapGet("/", async (bool? includeInactive, string? search, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAssociateGroupsQuery(includeInactive ?? false, search), ct))
            .WithName("Core_AssociateGroups_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetAssociateGroupQuery(id), ct))
            .WithName("Core_AssociateGroups_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (GrupoDelAsociadoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateAssociateGroupCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.Description)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/core/associate-groups/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Core_AssociateGroups_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, GrupoDelAsociadoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateAssociateGroupCommand(id, body.Name ?? string.Empty, body.Description)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_AssociateGroups_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/deactivate", async (Guid id, MotivoDeGrupoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetAssociateGroupActiveCommand(id, false, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_AssociateGroups_Deactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/reactivate", async (Guid id, MotivoDeGrupoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetAssociateGroupActiveCommand(id, true, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_AssociateGroups_Reactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);
    }

    public sealed record GrupoDelAsociadoRequest(string? Code, string? Name, string? Description);

    public sealed record MotivoDeGrupoRequest(string? Reason);
}