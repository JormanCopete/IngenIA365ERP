using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Alerts.AttendAlert;
using IngenIA365ERP.Application.Common.Alerts.GetAlert;
using IngenIA365ERP.Application.Common.Alerts.GetAlertTypeHistory;
using IngenIA365ERP.Application.Common.Alerts.ListAlerts;
using IngenIA365ERP.Application.Common.Alerts.ListAlertTypes;
using IngenIA365ERP.Application.Common.Alerts.SaveAlertType;
using IngenIA365ERP.Domain.Enums.Alerts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// La bandeja y la configuración de alertas (feature 012, T39, T095; contracts/api.md §16.1, §16.2). Ver y consultar
/// exigen <c>Inventory.Alerts.View</c>, atender <c>.Attend</c> y configurar tipos <c>.Manage</c>; los POST llevan
/// <c>Idempotency-Key</c>. Levantar una alerta no tiene ruta: lo hacen los procesos con <c>RaiseAlertCommand</c>. Cada
/// ruta sólo reenvía al <see cref="ISender"/>. Único creador de este archivo: las fases 11 y 21 no lo reescriben. Los
/// permisos los siembra el catálogo de Inventario (fase 3, T48); hasta entonces sólo entra el administrador maestro.
/// </summary>
public class AlertsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory Alerts")
            .RequireAuthorization();

        // ------------------------------------------------------------------------------------ bandeja §16.1 --

        group.MapGet("/alerts", async ([AsParameters] ListAlertsQuery query, ISender sender, CancellationToken ct) =>
                await sender.Send(query, ct))
            .WithName("Inventory_Alerts_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Alerts.View");

        group.MapGet("/alerts/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetAlertQuery(id), ct))
            .WithName("Inventory_Alerts_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Alerts.View");

        group.MapPost("/alerts/{id:guid}/attend", async (Guid id, AtenderRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new AttendAlertCommand(id, body.Note ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Alerts_Attend")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.Alerts.Attend");

        // ----------------------------------------------------------------------------------- tipos §16.2 --

        group.MapGet("/alert-types", async ([AsParameters] ListAlertTypesQuery query, ISender sender, CancellationToken ct) =>
                await sender.Send(query, ct))
            .WithName("Inventory_AlertTypes_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Alerts.View");

        group.MapGet("/alert-types/{typeCode}/history", async (string typeCode, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetAlertTypeHistoryQuery(typeCode), ct))
            .WithName("Inventory_AlertTypes_History")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Alerts.View");

        group.MapPost("/alert-types/{typeCode}/versions", async (string typeCode, VersionDeTipoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var command = new SaveAlertTypeCommand(
                    typeCode,
                    body.RecipientPermissions ?? [],
                    body.Channels ?? [],
                    body.Thresholds,
                    body.ValidFrom,
                    body.Reason ?? string.Empty,
                    body.IsEnabled ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                };
                var result = await sender.Send(command, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/alert-types/{Uri.EscapeDataString(typeCode)}/history", result.Value)
                    : result;
            })
            .WithName("Inventory_AlertTypes_Version")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.Alerts.Manage");
    }

    /// <summary>El cuerpo de <c>attend</c> (§16.1).</summary>
    public sealed record AtenderRequest(string? Note);

    /// <summary>El cuerpo de una versión de tipo (§16.2); <c>isEnabled</c> falso deja de levantarla desde la fecha.</summary>
    public sealed record VersionDeTipoRequest(
        IReadOnlyList<string>? RecipientPermissions,
        IReadOnlyList<AlertChannels>? Channels,
        IReadOnlyDictionary<string, decimal>? Thresholds,
        DateOnly ValidFrom,
        string? Reason,
        bool? IsEnabled);
}
