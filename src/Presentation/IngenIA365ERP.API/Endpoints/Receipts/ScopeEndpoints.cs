using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Approvals.RequestPresenceChallenge;
using IngenIA365ERP.Application.Receipts.Security;
using IngenIA365ERP.Domain.Enums.Approvals;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// El alcance de Recaudo de un usuario (feature 015, US12, T0059; contracts/api.md §2) y la aprobación presente de la caja (T0061; api.md §1.4): el desafío
/// del aprobador y su decisión, en el equipo del cajero, sobre las mismas piezas de la 012 (<see cref="RequestPresenceChallengeCommand"/>,
/// <see cref="DecideApprovalCommand"/>) pero con el permiso de quien opera la caja, no el de la bandeja de Inventario. Las fallas del desafío son 422 con
/// los <c>Approvals.Presence.*</c> de la 012, nunca 401. (nuevo)
/// </summary>
public class ScopeEndpoints : ICarterModule
{
    public const string PermisoDeAlcance = "Receipts.Scope.Manage";

    /// <summary>Quien pide la aprobación presente es quien opera la caja: el que abre su sesión.</summary>
    public const string PermisoDeLaCaja = "Receipts.Session.Open";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var usuarios = ReceiptsGroup.Grupo(app, "/users", "Recaudo Alcance");
        // Decisión del dueño del 2026-10-23 (DT §2.7): la lista de usuarios de las pantallas de configuración, con el permiso de Recaudo.
        usuarios.MapGet("/", async (string? search, ISender s, CancellationToken ct) => await s.Send(new ListReceiptsUsersQuery(search), ct))
            .WithName("Receipts_Users_List").RequirePermission(SetupEndpoints.PermisoDeConsulta);
        usuarios.MapGet("/{userId:guid}/scope", async (Guid userId, ISender s, CancellationToken ct) => await s.Send(new GetReceiptsUserScopeQuery(userId), ct))
            .WithName("Receipts_UserScope_Get").RequirePermission(PermisoDeAlcance);
        usuarios.MapPut("/{userId:guid}/scope", async (Guid userId, AlcanceRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SetReceiptsUserScopeCommand(userId, body.CollectionPointPublicIds ?? [], body.CashBoxPublicIds ?? [],
                    body.PaymasterPersonPublicIds ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_UserScope_Set").ConClaveDeOperacion().RequirePermission(PermisoDeAlcance);

        var aprobaciones = ReceiptsGroup.Grupo(app, "/approvals", "Recaudo Aprobación presente");
        aprobaciones.MapPost("/{id:guid}/presence-challenge", async (Guid id, DesafioRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RequestPresenceChallengeCommand(id, body.ApproverEmail ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Approvals_PresenceChallenge").ConClaveDeOperacion().RequirePermission(PermisoDeLaCaja);
        aprobaciones.MapPost("/{id:guid}/decide", async (Guid id, DecisionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DecideApprovalCommand(id, body.Decision, body.Reason, body.Method ?? ApprovalMethod.InPersonPasskey, body.Presence,
                    body.ExpectedContentSha256 ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Approvals_Decide").ConClaveDeOperacion().RequirePermission(PermisoDeLaCaja);
    }

    public sealed record AlcanceRequest(IReadOnlyList<Guid>? CollectionPointPublicIds, IReadOnlyList<Guid>? CashBoxPublicIds, IReadOnlyList<Guid>? PaymasterPersonPublicIds);

    public sealed record DesafioRequest(string? ApproverEmail);

    public sealed record DecisionRequest(ApprovalDecisionKind Decision, string? Reason, ApprovalMethod? Method, PresenciaDto? Presence, string? ExpectedContentSha256);
}
