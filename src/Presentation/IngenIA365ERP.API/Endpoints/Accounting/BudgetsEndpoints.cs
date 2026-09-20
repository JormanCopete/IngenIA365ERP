using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Accounting.Budgets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// Presupuesto por año (feature 009 E2 / US9, contracts/api.md §10): consulta, alta, versiones,
/// aprobación, copia del año anterior y distribución mensual. La ejecución no vive aquí sino en
/// <c>/api/reports/accounting/budget-execution</c>, porque es un informe con los mismos filtros
/// que los demás. Los cuerpos no llevan el año: lo trae la ruta, y aquí se arma el comando.
/// </summary>
public class BudgetsEndpoints : ICarterModule
{
    /// <summary>Cuerpo de <c>POST /</c>: el año va en el cuerpo porque todavía no hay recurso.</summary>
    public sealed record CrearPresupuestoRequest(int Year, IReadOnlyList<BudgetLineInput> Lines);

    /// <summary>Cuerpo de <c>PUT /{year}</c>: sobre un presupuesto aprobado <c>Reason</c> es obligatorio y nace una versión.</summary>
    public sealed record ActualizarPresupuestoRequest(IReadOnlyList<BudgetLineInput> Lines, string? Reason);

    /// <summary>Cuerpo de <c>POST /{year}/distribute</c>. <c>Mode</c>: <c>equal|manual|percent</c>; <c>Values</c> son los 12 meses en <c>manual</c> y <c>percent</c>.</summary>
    public sealed record DistribuirPresupuestoRequest(Guid AccountPublicId, Guid? BranchPublicId, Guid? CostCenterPublicId,
        decimal Total, string Mode, IReadOnlyList<decimal>? Values, string? Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/budgets")
            .WithTags("AccountingBudgets")
            .RequireAuthorization();

        // Sin presupuesto para el año devuelve 200 con Status «None»: la pantalla lo usa para ofrecer «Crear» o «Copiar».
        group.MapGet("/", async ([FromQuery] int year, [FromQuery] int? version, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetBudgetQuery(year, version), ct))
            .WithName("Accounting_Budgets_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Budget.View");

        group.MapPost("/", async (CrearPresupuestoRequest body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateBudgetCommand(body.Year, body.Lines ?? []), ct);
                return result.IsSuccess ? (object)Results.Created($"/api/accounting/budgets?year={result.Value.Year}", result.Value) : result;
            })
            .WithName("Accounting_Budgets_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Budget.Manage");

        group.MapPut("/{year:int}", async (int year, ActualizarPresupuestoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateBudgetCommand(year, body.Lines ?? [], body.Reason), ct))
            .WithName("Accounting_Budgets_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Budget.Manage");

        group.MapPost("/{year:int}/approve", async (int year, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApproveBudgetCommand(year), ct))
            .WithName("Accounting_Budgets_Approve")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Budget.Manage");

        // adjustPercent en puntos porcentuales (5 = +5 %); sin él, copia tal cual.
        group.MapPost("/{year:int}/copy-from/{previousYear:int}", async (int year, int previousYear, [FromQuery] decimal? adjustPercent, ISender sender, CancellationToken ct) =>
                await sender.Send(new CopyBudgetCommand(year, previousYear, adjustPercent ?? 0), ct))
            .WithName("Accounting_Budgets_CopyFrom")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Budget.Manage");

        group.MapPost("/{year:int}/distribute", async (int year, DistribuirPresupuestoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DistributeBudgetCommand(year, body.AccountPublicId, body.BranchPublicId, body.CostCenterPublicId,
                    body.Total, body.Mode ?? string.Empty, body.Values, body.Reason), ct))
            .WithName("Accounting_Budgets_Distribute")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Budget.Manage");
    }
}
