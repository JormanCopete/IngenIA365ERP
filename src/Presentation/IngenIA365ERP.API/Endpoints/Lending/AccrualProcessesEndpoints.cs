using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Approvals.RequestPresenceChallenge;
using IngenIA365ERP.Application.Lending.AccrualProcesses;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El proceso de causación (2026-11-16, pedido del dueño; feature 024, fase 3): contabilizar a mano, por rango y concepto, lo causado de los conceptos en
/// modo manual, con un borrador que se revisa y se recalcula y una puesta en firme de cuatro ojos. Ver (con los créditos por su número) exige
/// <c>Lending.AccrualProcesses.Read</c>; armar, recalcular y anular, <c>.Calculate</c>; pedir la firma, <c>.Confirm</c> (y un reproceso, además,
/// <c>.Reprocess</c>, que lo revisa el comando); reprocesar uno en firme, <c>.Reprocess</c>. La aprobación la da otra persona presente en el equipo de
/// quien pidió la firma, con su passkey o su TOTP, sobre las piezas de la plataforma (<see cref="RequestPresenceChallengeCommand"/>,
/// <see cref="DecideApprovalCommand"/>) y con el permiso de quien pide; el aprobador necesita <c>Lending.AccrualProcesses.Confirm</c> y lo revisa el motor.
/// Toda escritura lleva <c>Idempotency-Key</c>; fuera del alcance, el mismo 404. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class AccrualProcessesEndpoints : ICarterModule
{
    public const string Leer = "Lending.AccrualProcesses.Read";
    public const string Calcular = "Lending.AccrualProcesses.Calculate";
    public const string PonerEnFirme = "Lending.AccrualProcesses.Confirm";
    public const string Reprocesar = "Lending.AccrualProcesses.Reprocess";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var procesos = LendingGroup.Grupo(app, "/accrual-processes", "Cartera Proceso de causación");

        procesos.MapGet("/", async (AccrualProcessStatus? status, DateOnly? from, DateOnly? to, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAccrualProcessesQuery(status, from, to, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_AccrualProcesses_List")
            .RequirePermission(Leer);

        procesos.MapGet("/options", async (ISender sender, CancellationToken ct) => await sender.Send(new GetAccrualProcessOptionsQuery(), ct))
            .WithName("Lending_AccrualProcesses_Options")
            .RequirePermission(Leer);

        procesos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetAccrualProcessQuery(id), ct))
            .WithName("Lending_AccrualProcesses_Get")
            .RequirePermission(Leer);

        procesos.MapGet("/{id:guid}/lines", async (Guid id, AccrualProcessItemStatus? status, AccrualExclusionReason? reason, string? factType, string? search,
                int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAccrualProcessLinesQuery(id, status, reason, factType, search, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_AccrualProcesses_Lines")
            .RequirePermission(Leer);

        procesos.MapPost("/", async (CreateAccrualProcessCommand body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct),
                    p => $"{LendingGroup.Prefijo}/accrual-processes/{p.PublicId}"))
            .WithName("Lending_AccrualProcesses_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Calcular);

        procesos.MapPost("/{id:guid}/recalculate", async (Guid id, RecalcularRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RecalculateAccrualProcessCommand(id, body?.Changes) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_AccrualProcesses_Recalculate")
            .ConClaveDeOperacion()
            .RequirePermission(Calcular);

        procesos.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidAccrualProcessCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_AccrualProcesses_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Calcular);

        procesos.MapPost("/{id:guid}/confirm", async (Guid id, FirmaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ConfirmAccrualProcessCommand(id, body.Reason ?? string.Empty, body.ExpectedContentSha256 ?? string.Empty)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_AccrualProcesses_Confirm")
            .ConClaveDeOperacion()
            .RequirePermission(PonerEnFirme);

        procesos.MapPost("/{id:guid}/reprocess", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ReprocessAccrualProcessCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct),
                    p => $"{LendingGroup.Prefijo}/accrual-processes/{p.PublicId}"))
            .WithName("Lending_AccrualProcesses_Reprocess")
            .ConClaveDeOperacion()
            .RequirePermission(Reprocesar);

        // La aprobación de cuatro ojos: el desafío del aprobador presente y su decisión, en el equipo de quien pidió la firma (el id es el de la solicitud).
        procesos.MapPost("/approvals/{id:guid}/presence-challenge", async (Guid id, DesafioRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RequestPresenceChallengeCommand(id, body.ApproverEmail ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_AccrualProcesses_PresenceChallenge")
            .ConClaveDeOperacion()
            .RequirePermission(PonerEnFirme);

        procesos.MapPost("/approvals/{id:guid}/decide", async (Guid id, DecisionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DecideApprovalCommand(id, body.Decision, body.Reason, body.Method ?? ApprovalMethod.InPersonPasskey, body.Presence,
                    body.ExpectedContentSha256 ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_AccrualProcesses_Decide")
            .ConClaveDeOperacion()
            .RequirePermission(PonerEnFirme);
    }

    /// <summary><c>{ changes? }</c>: sin cambios, se recalcula con lo mismo.</summary>
    public sealed record RecalcularRequest(AccrualProcessInput? Changes);

    /// <summary>El motivo de anular o reprocesar.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary><c>{ reason, expectedContentSha256 }</c>: el motivo y la huella del borrador que se revisó.</summary>
    public sealed record FirmaRequest(string? Reason, string? ExpectedContentSha256);

    /// <summary>El correo del aprobador presente.</summary>
    public sealed record DesafioRequest(string? ApproverEmail);

    /// <summary>La decisión del aprobador presente con su prueba.</summary>
    public sealed record DecisionRequest(ApprovalDecisionKind Decision, string? Reason, ApprovalMethod? Method, PresenciaDto? Presence, string? ExpectedContentSha256);
}
