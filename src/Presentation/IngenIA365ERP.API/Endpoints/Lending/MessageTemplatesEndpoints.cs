using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las plantillas de mensaje al deudor (feature 013, T0309, T76; contracts/api.md §14.1) sobre <c>COR_NotificationTemplates</c> (módulo
/// <c>Lending</c>), versionadas: listar, historial por código, versión nueva (<c>CreateMessageTemplateVersionCommand</c>, que api.md llama
/// <c>AddMessageTemplateVersionCommand</c>) y la vista previa, que es una consulta por POST y va sin clave (§1.5). Ver y la vista previa
/// exigen <c>Lending.MessageTemplates.View</c>; la versión, <c>.Manage</c> con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class MessageTemplatesEndpoints : ICarterModule
{
    public const string Ver = "Lending.MessageTemplates.View";
    public const string Administrar = "Lending.MessageTemplates.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var plantillas = LendingGroup.Grupo(app, "/message-templates", "Cartera Plantillas de mensaje");

        plantillas.MapGet("/", async (DebtorMessagePurpose? purpose, MessageChannel? channel, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMessageTemplatesQuery(purpose, channel, date), ct))
            .WithName("Lending_MessageTemplates_List")
            .RequirePermission(Ver);

        plantillas.MapGet("/{code}/history", async (string code, MessageChannel? channel, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMessageTemplateHistoryQuery(code, channel), ct))
            .WithName("Lending_MessageTemplates_History")
            .RequirePermission(Ver);

        plantillas.MapPost("/{code}/versions", async (string code, VersionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateMessageTemplateVersionCommand(
                    code, body.Purpose, body.Channel, body.Subject, body.Body ?? string.Empty, body.Variables, body.ProviderTemplateId, body.ValidFrom,
                    body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/message-templates/{Uri.EscapeDataString(code)}/history"))
            .WithName("Lending_MessageTemplates_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        plantillas.MapPost("/preview", async (VistaPreviaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new RenderMessageTemplatePreviewQuery(
                    body.Channel, body.Code, body.Body, body.Purpose, body.Subject, body.SampleLoanPublicId, body.ProviderTemplateId), ct))
            .WithName("Lending_MessageTemplates_Preview")
            .RequirePermission(Ver);
    }

    /// <summary>El cuerpo de una versión nueva (§14.1); el código va en la ruta.</summary>
    public sealed record VersionRequest(
        DebtorMessagePurpose Purpose,
        MessageChannel Channel,
        string? Subject,
        string? Body,
        IReadOnlyList<string>? Variables,
        string? ProviderTemplateId,
        DateOnly ValidFrom,
        string? Reason);

    /// <summary>El cuerpo de la vista previa (§14.1): una plantilla guardada por código o un cuerpo en edición.</summary>
    public sealed record VistaPreviaRequest(
        MessageChannel Channel,
        string? Code,
        string? Body,
        DebtorMessagePurpose? Purpose,
        string? Subject,
        Guid? SampleLoanPublicId,
        string? ProviderTemplateId);
}
