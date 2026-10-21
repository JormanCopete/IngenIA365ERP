using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// La cola de mensajes al deudor (feature 013, T0309, C108, T74, T75, FR-122; contracts/api.md §14.2, adaptadores-y-archivos.md §4.6):
/// el registro de envíos con su historia, el envío manual o programado dentro de la ventana legal (Ley 2300), la cancelación de uno que no
/// salió y la ventana permitida (<c>/contact-window</c>, que no escribe). Ver exige <c>Lending.DebtorMessages.Read</c>, enviar <c>.Send</c> y
/// cancelar <c>.Cancel</c>; las escrituras llevan <c>Idempotency-Key</c> (en el envío es también la entidad de la clave del mensaje). Todo
/// filtra por el alcance de Cartera. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DebtorMessagesEndpoints : ICarterModule
{
    public const string Leer = "Lending.DebtorMessages.Read";
    public const string Enviar = "Lending.DebtorMessages.Send";
    public const string Cancelar = "Lending.DebtorMessages.Cancel";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var mensajes = LendingGroup.Grupo(app, "/debtor-messages", "Cartera Mensajes al deudor");

        mensajes.MapGet("/", async ([AsParameters] ListDebtorMessagesQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_DebtorMessages_List")
            .RequirePermission(Leer);

        mensajes.MapGet("/contact-window", async (Guid person, MessageChannel channel, DebtorMessagePurpose purpose, DateTimeOffset? at, Guid? loan,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new GetContactWindowQuery(person, channel, purpose, at, loan), ct))
            .WithName("Lending_DebtorMessages_ContactWindow")
            .RequirePermission(Leer);

        mensajes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetDebtorMessageQuery(id), ct))
            .WithName("Lending_DebtorMessages_Get")
            .RequirePermission(Leer);

        mensajes.MapPost("/", async (EnviarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new SendDebtorMessageCommand(
                    body.LoanPublicId, body.RecipientPersonPublicId, body.Channel, body.Purpose, body.TemplateCode ?? string.Empty, body.ScheduledFor,
                    body.Variables)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), m => $"/api/lending/debtor-messages/{m.PublicId}"))
            .WithName("Lending_DebtorMessages_Send")
            .ConClaveDeOperacion()
            .RequirePermission(Enviar);

        mensajes.MapPost("/{id:guid}/cancel", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CancelDebtorMessageCommand(id, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_DebtorMessages_Cancel")
            .ConClaveDeOperacion()
            .RequirePermission(Cancelar);
    }

    /// <summary>El cuerpo del envío manual (§14.2); sin <c>scheduledFor</c> es inmediato.</summary>
    public sealed record EnviarRequest(
        Guid? LoanPublicId,
        Guid RecipientPersonPublicId,
        MessageChannel Channel,
        DebtorMessagePurpose Purpose,
        string? TemplateCode,
        DateTimeOffset? ScheduledFor,
        IReadOnlyDictionary<string, string?>? Variables);

    /// <summary>Un motivo.</summary>
    public sealed record MotivoRequest(string? Reason);
}
