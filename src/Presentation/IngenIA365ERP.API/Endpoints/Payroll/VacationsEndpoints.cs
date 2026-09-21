using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Settlements.Vacation;
using IngenIA365ERP.Application.Payroll.Vacations;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Feature 010 US4. Dos grupos (contracts/api.md §5 y §3.3): el saldo derivado, los movimientos,
/// la vista previa de hábiles, los ajustes y la anulación viven en <c>/api/payroll/vacations</c>
/// (<c>Payroll.Vacations.View|Register</c>); la liquidación —registrar el disfrute o la
/// compensación crea el movimiento y la corrida <c>Vacation</c> en una acción— en
/// <c>/api/payroll/settlements/vacations</c> (<c>Payroll.Vacations.Calculate|Approve|Reverse</c>).
/// Los endpoints sólo reenvían al <c>ISender</c> (Principio III).
/// </summary>
public sealed class VacationsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // ------------------------------------------------------ saldo y movimientos (§5) --
        var vacaciones = app.MapGroup("/api/payroll/vacations")
            .WithTags("PayrollVacations")
            .RequireAuthorization();

        vacaciones.MapGet("/balances", async (DateOnly? asOf, string? search, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetVacationBalancesQuery(asOf, search), ct))
            .WithName("Payroll_Vacations_Balances")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.View");

        vacaciones.MapGet("/employees/{employeeId:guid}/balance", async (Guid employeeId, DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetEmployeeVacationBalanceQuery(employeeId, asOf), ct))
            .WithName("Payroll_Vacations_EmployeeBalance")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.View");

        vacaciones.MapGet("/employees/{employeeId:guid}/movements", async (Guid employeeId, bool? includeCancelled, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetEmployeeVacationMovementsQuery(employeeId, includeCancelled ?? true), ct))
            .WithName("Payroll_Vacations_EmployeeMovements")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.View");

        vacaciones.MapPost("/working-days", async (WorkingDaysBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewWorkingDaysQuery(body.From, body.To, body.EmployeePublicId), ct))
            .WithName("Payroll_Vacations_WorkingDays")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.Register");

        vacaciones.MapPost("/employees/{employeeId:guid}/adjustments", async (Guid employeeId, AdjustmentBody body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new AddVacationAdjustmentCommand(employeeId, body.Days, body.Reason), ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/vacations/employees/{employeeId}/movements", new { movementPublicId = result.Value })
                    : (object)result;
            })
            .WithName("Payroll_Vacations_AddAdjustment")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.Register");

        vacaciones.MapPost("/movements/{movementId:guid}/cancel", async (Guid movementId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new CancelVacationMovementCommand(movementId, body.Reason), ct))
            .WithName("Payroll_Vacations_CancelMovement")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.Register");

        // -------------------------------------------------------------- liquidación (§3.3) --
        var liquidaciones = app.MapGroup("/api/payroll/settlements/vacations")
            .WithTags("PayrollSettlementsVacations")
            .RequireAuthorization();

        liquidaciones.MapGet("/", async (Guid? employeeId, int? year, string? status, bool? includeSuperseded, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListVacationRunsQuery(employeeId, year, status, includeSuperseded ?? false), ct))
            .WithName("Payroll_Settlements_Vacations_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.View");

        liquidaciones.MapPost("/", async (CalculateVacationCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/runs/{result.Value.RunPublicId}", result.Value) : (object)result;
            })
            .WithName("Payroll_Settlements_Vacations_Calculate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.Calculate");

        liquidaciones.MapPost("/{runId:guid}/recalculate", async (Guid runId, RecalculateBody? body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RecalculateVacationCommand(runId, body?.AcceptRetroactive ?? false), ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/runs/{result.Value.RunPublicId}", result.Value) : (object)result;
            })
            .WithName("Payroll_Settlements_Vacations_Recalculate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.Calculate");

        liquidaciones.MapPost("/{runId:guid}/approve", async (Guid runId, ApproveBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApproveVacationCommand(runId, body.Confirm, body.PostingDate, body.ConfirmEmpty, body.ConfirmWithoutSegregation, body.AcceptRetroactive), ct))
            .WithName("Payroll_Settlements_Vacations_Approve")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.Approve");

        liquidaciones.MapPost("/{runId:guid}/reverse", async (Guid runId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReverseVacationCommand(runId, body.Reason), ct))
            .WithName("Payroll_Settlements_Vacations_Reverse")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.Reverse");

        // Mismo permiso que calcular: quien puede crear el borrador puede desecharlo (como la ordinaria).
        liquidaciones.MapPost("/{runId:guid}/discard", async (Guid runId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardVacationCommand(runId, body.Reason), ct))
            .WithName("Payroll_Settlements_Vacations_Discard")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Vacations.Calculate");
    }

    public sealed record WorkingDaysBody(DateOnly From, DateOnly To, Guid? EmployeePublicId = null);
    public sealed record AdjustmentBody(decimal Days, string Reason);
    public sealed record ReasonBody(string Reason);
    public sealed record RecalculateBody(bool AcceptRetroactive = false);
    public sealed record ApproveBody(bool Confirm, DateOnly? PostingDate = null, bool ConfirmEmpty = false, bool ConfirmWithoutSegregation = false, bool AcceptRetroactive = false);
}
