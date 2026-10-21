using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Sesiones y actas de los órganos (feature 013, T0313, T64, FR-039, FR-040; contracts/api.md §5): convocar, asistencia, puntos, votos con
/// presencia (passkey o TOTP, base64url por <c>wwwroot/js/webauthn.js</c>), cierre sellado y el PDF del acta. Ver exige
/// <c>Lending.Sessions.Read</c> (la lectura del acta queda auditada como <c>Lending.Session.Viewed</c>); convocar, asistencia y puntos
/// <c>.Manage</c>; cerrar <c>.Close</c>. Votar se abre con <c>.Read</c> y el handler exige además <c>.RecordVotes</c> (el de cualquier
/// miembro) o <c>.Vote</c> (el propio): sin ninguno, 422 <c>Lending.Permission.Required</c> (§1.4). Toda escritura lleva
/// <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class SessionsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Sessions.Read";
    public const string Administrar = "Lending.Sessions.Manage";
    public const string Cerrar = "Lending.Sessions.Close";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var sesiones = LendingGroup.Grupo(app, "/sessions", "Cartera Sesiones");

        sesiones.MapGet("/", async ([AsParameters] ListSessionsQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_Sessions_List")
            .RequirePermission(Leer);

        // El selector del «punto de acta» (§1.9): puntos de actas cerradas.
        sesiones.MapGet("/items", async ([AsParameters] ListSessionItemsQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_Sessions_Items")
            .RequirePermission(Leer);

        sesiones.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetSessionQuery(id), ct))
            .WithName("Lending_Sessions_Get")
            .RequirePermission(Leer);

        sesiones.MapGet("/{id:guid}/minutes.pdf", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var acta = await sender.Send(new GetSessionMinutesPdfQuery(id), ct);
                return acta.IsSuccess ? (object)Results.File(acta.Value.Contenido, "application/pdf", acta.Value.NombreArchivo) : acta;
            })
            .WithName("Lending_Sessions_MinutesPdf")
            .RequirePermission(Leer);

        sesiones.MapPost("/", async (ConvocarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateSessionCommand(
                    body.GoverningBodyPublicId, body.Kind, body.Mode, body.HeldOn, body.Location, body.Notes)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), s => $"/api/lending/sessions/{s.PublicId}"))
            .WithName("Lending_Sessions_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        sesiones.MapPost("/{id:guid}/attendance", async (Guid id, AsistenciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RecordSessionAttendanceCommand(id, body.Attendees ?? [], body.Guests)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Sessions_Attendance")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        sesiones.MapPost("/{id:guid}/items", async (Guid id, PuntoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddSessionItemCommand(
                    id, body.Subject ?? string.Empty, body.SubjectPublicId, body.Kind, body.Notes, body.RecommendsToBodyPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/sessions/{id}"))
            .WithName("Lending_Sessions_AddItem")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        sesiones.MapPut("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, CorregirPuntoRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new UpdateSessionItemCommand(id, itemId, body.ProposedOutcome, body.Conditions, body.Notes)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Sessions_UpdateItem")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        sesiones.MapDelete("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, [FromBody] MotivoRequest? body, string? reason,
                HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RemoveSessionItemCommand(id, itemId, body?.Reason ?? reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Sessions_RemoveItem")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        sesiones.MapPost("/{id:guid}/presence-challenge", async (Guid id, DesafioRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CreateSessionPresenceChallengeCommand(id, body.PersonPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Sessions_PresenceChallenge")
            .ConClaveDeOperacion()
            .RequirePermission(Leer);

        sesiones.MapPost("/{id:guid}/items/{itemId:guid}/votes", async (Guid id, Guid itemId, VotoRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RecordSessionVoteCommand(
                    id, itemId, body.PersonPublicId, body.Vote, body.Method, body.Presence, body.ReplacementReason)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/sessions/{id}"))
            .WithName("Lending_Sessions_Vote")
            .ConClaveDeOperacion()
            .RequirePermission(Leer);

        sesiones.MapPost("/{id:guid}/close", async (Guid id, CerrarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseSessionCommand(id, body.ExpectedContentSha256 ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Sessions_Close")
            .ConClaveDeOperacion()
            .RequirePermission(Cerrar);
    }

    /// <summary>El cuerpo de la convocatoria (§5): la sesión nace en <c>Draft</c>.</summary>
    public sealed record ConvocarRequest(
        Guid GoverningBodyPublicId,
        SessionKind Kind,
        SessionMode Mode,
        DateOnly HeldOn,
        string? Location,
        string? Notes);

    /// <summary>La asistencia (§5); los invitados van por su persona y no cuentan para el quórum.</summary>
    public sealed record AsistenciaRequest(IReadOnlyList<AttendeeInput>? Attendees, IReadOnlyList<Guid>? Guests);

    /// <summary>Un punto (§5): el sujeto y su <c>PublicId</c>; la regla y los excluidos los calcula el sistema.</summary>
    public sealed record PuntoRequest(
        string? Subject,
        Guid SubjectPublicId,
        SessionItemKind Kind,
        string? Notes,
        Guid? RecommendsToBodyPublicId);

    /// <summary>La corrección de un punto abierto (§5).</summary>
    public sealed record CorregirPuntoRequest(CreditDecisionOutcome? ProposedOutcome, string? Conditions, string? Notes);

    /// <summary>Un motivo.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>A quién se le pide la presencia (§5).</summary>
    public sealed record DesafioRequest(Guid PersonPublicId);

    /// <summary>Un voto (§5); el de reemplazo lleva su motivo.</summary>
    public sealed record VotoRequest(
        Guid PersonPublicId,
        VoteValue Vote,
        VoteMethod Method,
        PresenciaDto? Presence,
        string? ReplacementReason);

    /// <summary>El cierre (§5): la huella del acta que se revisó (<c>currentContentSha256</c> del <c>GET</c>).</summary>
    public sealed record CerrarRequest(string? ExpectedContentSha256);
}
