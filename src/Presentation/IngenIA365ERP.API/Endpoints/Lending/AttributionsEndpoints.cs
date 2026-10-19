using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.ListApprovalPolicies;
using IngenIA365ERP.Application.Common.Approvals.ListPermissionAmountLimits;
using IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;
using IngenIA365ERP.Application.Common.Approvals.SetPermissionAmountLimit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las atribuciones de Cartera (feature 013, T0313, T63, T65; contracts/api.md §3.5): la matriz de atribuciones versionada
/// (<c>/attribution-matrices</c>) y los montos de las atribuciones operativas (<c>/amount-limits</c>, sobre <c>SEC_PermissionAmountLimits</c>
/// con <c>Module = "Lending"</c>: sólo los cuatro códigos limitables de Cartera). Leer y la vista previa exigen
/// <c>Lending.Attributions.View</c>; publicar y fijar montos, <c>Lending.Attributions.Manage</c> con <c>Idempotency-Key</c>. La vista previa
/// es una consulta por POST y va sin clave (§1.5). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class AttributionsEndpoints : ICarterModule
{
    public const string Ver = "Lending.Attributions.View";
    public const string Administrar = "Lending.Attributions.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ------------------------------------------------------------------------------ matriz §3.5.1 --
        var matrices = LendingGroup.Grupo(app, "/attribution-matrices", "Cartera Atribuciones");

        matrices.MapGet("/", async (DateOnly? date, bool? includeHistory, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAttributionMatricesQuery(date, includeHistory ?? false), ct))
            .WithName("Lending_AttributionMatrices_List")
            .RequirePermission(Ver);

        matrices.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetAttributionMatrixQuery(id), ct))
            .WithName("Lending_AttributionMatrices_Get")
            .RequirePermission(Ver);

        matrices.MapPost("/", async (PublicarMatrizRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddAttributionMatrixVersionCommand(
                    body.ValidFrom, body.Reason ?? string.Empty, body.Source, body.Levels ?? [], body.Rules ?? [])
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), m => $"/api/lending/attribution-matrices/{m.PublicId}"))
            .WithName("Lending_AttributionMatrices_Publish")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        matrices.MapPost("/preview", async (VistaPreviaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewAttributionRoutingQuery(
                    body.Subject ?? string.Empty, body.Amount, body.CreditLinePublicId, body.PersonPublicId, body.TermInstallments,
                    body.CollateralKinds, body.Score, body.Alerts, body.DaysPastDue, body.Concept, body.PolicyException ?? false, body.Date,
                    body.NoveltyMechanism, body.NoveltyCause), ct))
            .WithName("Lending_AttributionMatrices_Preview")
            .RequirePermission(Ver);

        // ---------------------------------------------------------------- montos operativos §3.5.2 --
        var montos = LendingGroup.Grupo(app, "/amount-limits", "Cartera Atribuciones");

        montos.MapGet("/", async (Guid? rolePublicId, string? permissionCode, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPermissionAmountLimitsQuery(rolePublicId, permissionCode, date, PermisosLimitables.ModuloCartera), ct))
            .WithName("Lending_AmountLimits_List")
            .RequirePermission(Ver);

        montos.MapPost("/", async (FijarMontoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new SetPermissionAmountLimitCommand(
                    body.RolePublicId, body.PermissionCode ?? string.Empty, body.MaxAmount, body.ValidFrom, body.Reason ?? string.Empty)
                {
                    Module = PermisosLimitables.ModuloCartera,
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/amount-limits?rolePublicId={body.RolePublicId}"))
            .WithName("Lending_AmountLimits_Set")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        // ------------------------------------- políticas de aprobación de las cuentas (feature 014, decisión del dueño del 2026-10-18, D53) --
        // El retiro y la consignación en efectivo sobre la atribución piden aprobación con los niveles que la cooperativa configure (umbral y permiso de
        // cada nivel), sobre la misma plataforma de Inventario; sin política rige la regla fija de un nivel.
        var politicas = LendingGroup.Grupo(app, "/approval-policies", "Cartera Atribuciones");

        politicas.MapGet("/", async (bool? includeHistory, DateOnly? asOf, ISender sender, CancellationToken ct) =>
            {
                var todas = new List<ApprovalPolicyDto>();
                foreach (var sujeto in SujetosDeCuentas)
                {
                    var r = await sender.Send(new ListApprovalPoliciesQuery(sujeto, null, asOf, includeHistory), ct);
                    if (r.IsFailure) return Result.Failure<IReadOnlyList<ApprovalPolicyDto>>(r.Error);
                    todas.AddRange(r.Value);
                }
                return Result.Success<IReadOnlyList<ApprovalPolicyDto>>(todas);
            })
            .WithName("Lending_ApprovalPolicies_List")
            .RequirePermission(Ver);

        politicas.MapPost("/", async (PoliticaDeCuentasRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                if (!SujetosDeCuentas.Contains(body.Subject ?? string.Empty, StringComparer.Ordinal))
                    return Result.Failure<ApprovalPolicyDto>(new ErrorConDatos("Lending.ApprovalPolicy.SubjectNotAllowed",
                        $"Desde la Cartera se configuran las políticas de {string.Join(" y ", SujetosDeCuentas)}.", new { allowed = SujetosDeCuentas }));
                return await sender.Send(new SaveApprovalPolicyCommand(body.Subject!, null, body.ValidFrom, body.Reason ?? string.Empty, body.Levels ?? [])
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
            })
            .WithName("Lending_ApprovalPolicies_Save")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>Feature 014 (D53, 2026-10-18): los sujetos de aprobación de las cuentas de captación que se configuran desde la Cartera.</summary>
    public static readonly IReadOnlyList<string> SujetosDeCuentas = [ApprovalSubjects.AccountWithdrawal, ApprovalSubjects.AccountCashDeposit];

    /// <summary>El cuerpo de una versión de política de un sujeto de cuentas (D53).</summary>
    public sealed record PoliticaDeCuentasRequest(string? Subject, DateOnly ValidFrom, string? Reason, IReadOnlyList<NivelDto>? Levels);

    /// <summary>El cuerpo de una versión completa de la matriz (§3.5.1).</summary>
    public sealed record PublicarMatrizRequest(
        DateOnly ValidFrom,
        string? Reason,
        string? Source,
        IReadOnlyList<AttributionLevelInput>? Levels,
        IReadOnlyList<AttributionRuleInput>? Rules);

    /// <summary>El cuerpo de la vista previa «¿a qué nivel iría?» (§3.5.1); el monto es el consolidado.</summary>
    public sealed record VistaPreviaRequest(
        string? Subject,
        decimal Amount,
        Guid? CreditLinePublicId,
        Guid? PersonPublicId,
        int? TermInstallments,
        IReadOnlyList<CollateralKind>? CollateralKinds,
        decimal? Score,
        IReadOnlyList<string>? Alerts,
        int? DaysPastDue,
        LoanConcept? Concept,
        bool? PolicyException,
        DateOnly? Date,
        NoveltyMechanism? NoveltyMechanism = null,
        NoveltyCause? NoveltyCause = null);

    /// <summary>El cuerpo del monto de una atribución operativa (§3.5.2); <c>maxAmount</c> nulo = sin límite.</summary>
    public sealed record FijarMontoRequest(Guid RolePublicId, string? PermissionCode, decimal? MaxAmount, DateOnly ValidFrom, string? Reason);
}
