using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Platform.Connectors;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Saas;

/// <summary>
/// Los conectores externos que define el maestro (feature 020, US7; R15; contracts/api.md §1): definición como dato, versiones inmutables,
/// simulación sin red y habilitación por cooperativa. Sólo del maestro con segundo factor (<see cref="RequireMasterAdminAttribute.Check"/>,
/// sin código de permiso). Publicar o habilitar no escribe en la base de ninguna cooperativa: la copia la deja al día la tarea de réplica.
/// </summary>
public sealed class ConnectorsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/saas/connectors")
            .WithTags("SaaS / Conectores")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .AddEndpointFilter(async (contexto, siguiente) =>
            {
                var guarda = RequireMasterAdminAttribute.Check(contexto.HttpContext);
                return guarda.IsAllowed ? await siguiente(contexto) : Result.Failure(guarda.ErrorCode!, guarda.Message!);
            });

        g.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new ListPlatformConnectorsQuery(), ct))
            .WithName("Saas_Connectors_List");

        g.MapPost("/", async (ConectorNuevoRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreatePlatformConnectorCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.Description), ct);
                return r.IsSuccess ? Results.Ok(new { publicId = r.Value }) : (object)r;
            })
            .WithName("Saas_Connectors_Create");

        g.MapPut("/{id:guid}", async (Guid id, ConectorCambioRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new UpdatePlatformConnectorCommand(id, body.Name ?? string.Empty, body.Description, body.IsActive), ct);
                return r.IsSuccess ? Results.NoContent() : (object)r;
            })
            .WithName("Saas_Connectors_Update");

        g.MapGet("/{id:guid}/versions", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListConnectorVersionsQuery(id), ct))
            .WithName("Saas_Connectors_Versions");

        g.MapPost("/{id:guid}/versions", async (Guid id, VersionNuevaRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new PublishConnectorVersionCommand(id, body.BaseUrl ?? string.Empty, TextoDeLaDefinicion(body.Definition), body.Note), ct);
                return r.IsSuccess ? Results.Ok(new { number = r.Value }) : (object)r;
            })
            .WithName("Saas_Connectors_Publish");

        g.MapPost("/{id:guid}/versions/{number:int}/simulate", async (Guid id, int number, SimulacionRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateConnectorQuery(id, number, body.Variables ?? new Dictionary<string, string>()), ct))
            .WithName("Saas_Connectors_Simulate");

        g.MapGet("/{id:guid}/tenants", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new ListConnectorTenantsQuery(id), ct))
            .WithName("Saas_Connectors_Tenants");

        g.MapPut("/{id:guid}/tenants/{tenantId:guid}", async (Guid id, Guid tenantId, HabilitacionRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new SetTenantConnectorCommand(id, tenantId, body.Enabled, body.Reason ?? string.Empty), ct);
                return r.IsSuccess ? Results.NoContent() : (object)r;
            })
            .WithName("Saas_Connectors_SetTenant");
    }

    public sealed record ConectorNuevoRequest(string? Code, string? Name, string? Description);

    public sealed record ConectorCambioRequest(string? Name, string? Description, bool IsActive);

    /// <summary>La definición llega como objeto JSON (contracts/api.md §1) o como texto con el JSON (lo que la pantalla tiene en su editor).</summary>
    public sealed record VersionNuevaRequest(string? BaseUrl, System.Text.Json.JsonElement? Definition, string? Note);

    private static string TextoDeLaDefinicion(System.Text.Json.JsonElement? definicion) => definicion switch
    {
        null => string.Empty,
        { ValueKind: System.Text.Json.JsonValueKind.String } d => d.GetString() ?? string.Empty,
        { ValueKind: System.Text.Json.JsonValueKind.Object } d => d.GetRawText(),
        _ => string.Empty,
    };

    public sealed record SimulacionRequest(Dictionary<string, string>? Variables);

    public sealed record HabilitacionRequest(bool Enabled, string? Reason);
}
