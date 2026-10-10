using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.ListPermissionAmountLimits;
using IngenIA365ERP.Application.Common.Approvals.SetPermissionAmountLimit;
using IngenIA365ERP.Application.Lending.Attributions;
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
                    body.CollateralKinds, body.Score, body.Alerts, body.DaysPastDue, body.Concept, body.PolicyException ?? false, body.Date), ct))
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
    }

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
        DateOnly? Date);

    /// <summary>El cuerpo del monto de una atribución operativa (§3.5.2); <c>maxAmount</c> nulo = sin límite.</summary>
    public sealed record FijarMontoRequest(Guid RolePublicId, string? PermissionCode, decimal? MaxAmount, DateOnly ValidFrom, string? Reason);
}
