using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Approvals.GetApprovalRequest;
using IngenIA365ERP.Application.Common.Approvals.ListApprovalPolicies;
using IngenIA365ERP.Application.Common.Approvals.ListMyPendingApprovals;
using IngenIA365ERP.Application.Common.Approvals.ListPermissionAmountLimits;
using IngenIA365ERP.Application.Common.Approvals.RequestPresenceChallenge;
using IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;
using IngenIA365ERP.Application.Common.Approvals.SetPermissionAmountLimit;
using IngenIA365ERP.Application.Common.Approvals.WithdrawApprovalRequest;
using IngenIA365ERP.Domain.Enums.Approvals;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Aprobaciones y montos máximos (feature 012, T33, T34, T086; contracts/api.md §15): un solo motor de plataforma para
/// todo lo que pide aprobación, sin rutas de aprobación por documento. Políticas y montos exigen
/// <c>Inventory.ApprovalPolicies.View</c>/<c>.Manage</c>; la bandeja y la decisión, <c>Inventory.Approvals.View</c>
/// —decidir revisa además el permiso del nivel adentro (§17.2 punto 4): sin él, el mismo 404—. Los POST llevan
/// <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. Los permisos los siembra el catálogo de
/// Inventario (fase 3, T48); hasta entonces sólo entra el administrador maestro.
/// </summary>
public class ApprovalsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory Approvals")
            .RequireAuthorization();

        // ------------------------------------------------------------------------------------ políticas §15.1 --

        group.MapGet("/approval-policies", async ([AsParameters] ListApprovalPoliciesQuery query, ISender sender, CancellationToken ct) =>
                await sender.Send(query, ct))
            .WithName("Inventory_ApprovalPolicies_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.ApprovalPolicies.View");

        group.MapPost("/approval-policies", async (GuardarPoliticaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var command = new SaveApprovalPolicyCommand(body.Subject ?? string.Empty, body.DocumentTypePublicId, body.ValidFrom,
                    body.Reason ?? string.Empty, body.Levels ?? [])
                {
                    OperationKey = http.ClaveDeOperacion(),
                };
                var result = await sender.Send(command, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/approval-policies?subject={result.Value.Subject}", result.Value)
                    : result;
            })
            .WithName("Inventory_ApprovalPolicies_Save")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.ApprovalPolicies.Manage");

        // --------------------------------------------------------------------------- bandeja y decisión §15.2 --

        group.MapGet("/approvals", async ([AsParameters] ListMyPendingApprovalsQuery query, ISender sender, CancellationToken ct) =>
                await sender.Send(query, ct))
            .WithName("Inventory_Approvals_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Approvals.View");

        group.MapGet("/approvals/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetApprovalRequestQuery(id), ct))
            .WithName("Inventory_Approvals_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Approvals.View");

        group.MapPost("/approvals/{id:guid}/decide", async (Guid id, DecidirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DecideApprovalCommand(id, body.Decision, body.Reason, body.Method ?? ApprovalMethod.OwnSession,
                    body.Presence, body.ExpectedContentSha256 ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Approvals_Decide")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.Approvals.View");

        group.MapPost("/approvals/{id:guid}/presence-challenge", async (Guid id, DesafioRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RequestPresenceChallengeCommand(id, body.ApproverEmail ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Approvals_PresenceChallenge")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.Approvals.View");

        group.MapPost("/approvals/{id:guid}/withdraw", async (Guid id, RetirarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new WithdrawApprovalRequestCommand(id, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Approvals_Withdraw")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.Approvals.View");

        // ------------------------------------------------------------------------------ montos máximos §15.3 --

        group.MapGet("/amount-limits", async ([AsParameters] ListPermissionAmountLimitsQuery query, ISender sender, CancellationToken ct) =>
                await sender.Send(query, ct))
            .WithName("Inventory_AmountLimits_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.ApprovalPolicies.View");

        group.MapPost("/amount-limits", async (FijarMontoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var command = new SetPermissionAmountLimitCommand(body.RolePublicId, body.PermissionCode ?? string.Empty, body.MaxAmount,
                    body.ValidFrom, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                };
                var result = await sender.Send(command, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/amount-limits?rolePublicId={body.RolePublicId}", result.Value)
                    : result;
            })
            .WithName("Inventory_AmountLimits_Set")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.ApprovalPolicies.Manage");
    }

    /// <summary>El cuerpo del alta de política (§15.1).</summary>
    public sealed record GuardarPoliticaRequest(
        string? Subject,
        Guid? DocumentTypePublicId,
        DateOnly ValidFrom,
        string? Reason,
        IReadOnlyList<NivelDto>? Levels);

    /// <summary>El cuerpo de <c>decide</c> (§15.2).</summary>
    public sealed record DecidirRequest(
        ApprovalDecisionKind Decision,
        string? Reason,
        ApprovalMethod? Method,
        PresenciaDto? Presence,
        string? ExpectedContentSha256);

    /// <summary>El cuerpo de <c>presence-challenge</c> (§15.2).</summary>
    public sealed record DesafioRequest(string? ApproverEmail);

    /// <summary>El cuerpo de <c>withdraw</c> (§15.2).</summary>
    public sealed record RetirarRequest(string? Reason);

    /// <summary>El cuerpo del monto máximo (§15.3).</summary>
    public sealed record FijarMontoRequest(
        Guid RolePublicId,
        string? PermissionCode,
        decimal? MaxAmount,
        DateOnly ValidFrom,
        string? Reason);
}
