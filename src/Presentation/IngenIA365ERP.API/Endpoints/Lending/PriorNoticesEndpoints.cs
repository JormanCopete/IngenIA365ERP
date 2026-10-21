using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections.PriorNotices;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los avisos previos al reporte negativo, uno por episodio (feature 013, K4, US12, T0956; contracts/api.md §14.3, FR-123, T77): la lista con el estado
/// derivado (<c>Lending.PriorNotices.Read</c>), enviar una comunicación por correo, SMS o WhatsApp (<c>Lending.PriorNotices.Send</c>) y registrar la
/// constancia de una carta con su guía (<c>Lending.PriorNotices.RegisterEvidence</c>). Las escrituras llevan <c>Idempotency-Key</c>. Cada ruta sólo
/// reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PriorNoticesEndpoints : ICarterModule
{
    public const string Leer = "Lending.PriorNotices.Read";
    public const string Enviar = "Lending.PriorNotices.Send";
    public const string Constancia = "Lending.PriorNotices.RegisterEvidence";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var avisos = LendingGroup.Grupo(app, "/prior-notices", "Cartera Avisos previos");

        avisos.MapGet("/", async ([AsParameters] ListPriorNoticesQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_PriorNotices_List")
            .RequirePermission(Leer);

        avisos.MapPost("/{id:guid}/send", async (Guid id, EnviarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new SendPriorNoticeCommand(id, body.Channel) { OperationKey = http.ClaveDeOperacion() }, ct),
                    m => $"/api/lending/debtor-messages/{m.PublicId}"))
            .WithName("Lending_PriorNotices_Send")
            .ConClaveDeOperacion()
            .RequirePermission(Enviar);

        avisos.MapPost("/{id:guid}/evidence", async (Guid id, ConstanciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RegisterPriorNoticeEvidenceCommand(id, body.CommunicationNumber, body.SentAt, body.Carrier ?? string.Empty,
                    body.TrackingNumber ?? string.Empty, body.AttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_PriorNotices_Evidence")
            .ConClaveDeOperacion()
            .RequirePermission(Constancia);
    }

    /// <summary>El canal de la comunicación (§14.3).</summary>
    public sealed record EnviarRequest(MessageChannel Channel);

    /// <summary>La constancia de una carta: número de comunicación, envío, transportadora, guía y el adjunto.</summary>
    public sealed record ConstanciaRequest(int CommunicationNumber, DateTimeOffset SentAt, string? Carrier, string? TrackingNumber, Guid AttachmentPublicId);
}
