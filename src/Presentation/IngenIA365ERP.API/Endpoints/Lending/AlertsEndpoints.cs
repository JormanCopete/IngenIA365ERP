using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Alerts.AttendAlert;
using IngenIA365ERP.Application.Common.Alerts.GetAlert;
using IngenIA365ERP.Application.Common.Alerts.ListAlerts;
using IngenIA365ERP.Application.Common.Alerts.ListAlertTypes;
using IngenIA365ERP.Application.Common.Alerts.SaveAlertType;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Domain.Enums.Alerts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// La bandeja y la configuración de alertas de Cartera (feature 013, T0313, C68; contracts/api.md §3.10) sobre las de la plataforma
/// (<c>COR_Alerts</c>), con módulo <c>Lending</c>: cada usuario ve las que lo alcanzan (permiso destinatario del tipo y alcance sobre la oficina
/// y la línea de la alerta, <c>VisibilidadDeAlertas</c>). Ver exige <c>Lending.Alerts.View</c>, atender <c>.Attend</c> y configurar tipos
/// <c>.Manage</c> (sólo los tipos del módulo <c>Lending</c>); las escrituras llevan <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class AlertsEndpoints : ICarterModule
{
    public const string Ver = "Lending.Alerts.View";
    public const string Atender = "Lending.Alerts.Attend";
    public const string Administrar = "Lending.Alerts.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var grupo = LendingGroup.Grupo(app, string.Empty, "Cartera Alertas");

        grupo.MapGet("/alerts", async (AlertStatus? status, string? typeCode, AlertSeverity? severity, DateOnly? from, DateOnly? to, int? page,
                int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAlertsQuery(status, typeCode, ModuloDeAuditoria.Lending, severity, from, to, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Alerts_List")
            .RequirePermission(Ver);

        grupo.MapGet("/alerts/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetAlertQuery(id), ct))
            .WithName("Lending_Alerts_Get")
            .RequirePermission(Ver);

        grupo.MapPost("/alerts/{id:guid}/attend", async (Guid id, AtenderRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new AttendAlertCommand(id, body.Note ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Alerts_Attend")
            .ConClaveDeOperacion()
            .RequirePermission(Atender);

        grupo.MapGet("/alert-types", async (DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAlertTypesQuery(date, ModuloDeAuditoria.Lending), ct))
            .WithName("Lending_AlertTypes_List")
            .RequirePermission(Ver);

        grupo.MapPost("/alert-types/{typeCode}/versions", async (string typeCode, VersionDeTipoRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new SaveAlertTypeCommand(
                    typeCode, body.RecipientPermissions ?? [], body.Channels ?? [], body.Thresholds, body.ValidFrom, body.Reason ?? string.Empty,
                    body.IsEnabled ?? true)
                {
                    Module = ModuloDeAuditoria.Lending,
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), "/api/lending/alert-types"))
            .WithName("Lending_AlertTypes_Version")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo de <c>attend</c> (§3.10).</summary>
    public sealed record AtenderRequest(string? Note);

    /// <summary>El cuerpo de una versión de tipo (§3.10).</summary>
    public sealed record VersionDeTipoRequest(
        IReadOnlyList<string>? RecipientPermissions,
        IReadOnlyList<AlertChannels>? Channels,
        IReadOnlyDictionary<string, decimal>? Thresholds,
        DateOnly ValidFrom,
        string? Reason,
        bool? IsEnabled);
}
