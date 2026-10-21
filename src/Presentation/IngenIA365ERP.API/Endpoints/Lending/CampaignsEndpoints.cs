using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections.Campaigns;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las campañas de mensajes de cobranza (feature 013, K4, US12, T0956; contracts/api.md §14.9): todo con <c>Lending.Campaigns.Manage</c>. Crear en
/// borrador, vista previa con destinatarios y excluidos por motivo (no escribe), programar (un mensaje por destinatario, con la constancia de
/// exclusión del que no cabe) y cancelar. Las escrituras llevan <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class CampaignsEndpoints : ICarterModule
{
    public const string Administrar = "Lending.Campaigns.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var campanas = LendingGroup.Grupo(app, "/campaigns", "Cartera Campañas");

        campanas.MapGet("/", async (MessageCampaignStatus? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMessageCampaignsQuery(status), ct))
            .WithName("Lending_Campaigns_List")
            .RequirePermission(Administrar);

        campanas.MapPost("/", async (CampanaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateMessageCampaignCommand(body.Code ?? string.Empty, body.Name ?? string.Empty,
                    body.TemplateCode ?? string.Empty, body.Channel, body.Purpose, body.Filters ?? new CampaignFilters(), body.ScheduledFor)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), c => $"/api/lending/campaigns/{c.PublicId}"))
            .WithName("Lending_Campaigns_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        campanas.MapPost("/{id:guid}/preview", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewMessageCampaignQuery(id), ct))
            .WithName("Lending_Campaigns_Preview")
            .RequirePermission(Administrar);

        campanas.MapPost("/{id:guid}/schedule", async (Guid id, MotivoRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ScheduleMessageCampaignCommand(id, body?.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Campaigns_Schedule")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        campanas.MapPost("/{id:guid}/cancel", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CancelMessageCampaignCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Campaigns_Cancel")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo de una campaña en borrador (§14.9).</summary>
    public sealed record CampanaRequest(
        string? Code,
        string? Name,
        string? TemplateCode,
        MessageChannel Channel,
        DebtorMessagePurpose Purpose,
        CampaignFilters? Filters,
        DateTimeOffset ScheduledFor);

    /// <summary>Un motivo.</summary>
    public sealed record MotivoRequest(string? Reason);
}
