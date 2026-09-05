using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 005 (contracts/api.md §4): calcular, revisar y aprobar la liquidación. Comparativo, cuadre, exportación y reversión se añaden en US4 y US7.</summary>
public sealed class PayrollRunsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll")
            .WithTags("PayrollRuns")
            .RequireAuthorization();

        group.MapPost("/pay-periods/{periodId:guid}/runs", async (Guid periodId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CalculatePayrollRunCommand(periodId), ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/runs/{result.Value.RunPublicId}", result.Value) : (object)result;
            })
            .WithName("Payroll_Runs_Calculate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Runs.Calculate");

        group.MapGet("/pay-periods/{periodId:guid}/runs/current", async (Guid periodId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCurrentRunQuery(periodId), ct))
            .WithName("Payroll_Runs_Current")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Runs.View");

        group.MapGet("/pay-periods/{periodId:guid}/runs", async (Guid periodId, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRunsQuery(periodId), ct))
            .WithName("Payroll_Runs_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Runs.View");

        group.MapGet("/runs/{runId:guid}", async (Guid runId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetRunSummaryQuery(runId), ct))
            .WithName("Payroll_Runs_Summary")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Runs.View");

        group.MapGet("/runs/{runId:guid}/employees", async (Guid runId, string? flag, bool? changed, string? search, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRunEmployeesQuery(runId, flag, changed, search), ct))
            .WithName("Payroll_Runs_Employees")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Runs.View");

        group.MapGet("/runs/{runId:guid}/employees/{employeeId:guid}", async (Guid runId, Guid employeeId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetRunEmployeeDetailQuery(runId, employeeId), ct))
            .WithName("Payroll_Runs_EmployeeDetail")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Runs.View");

        group.MapPost("/runs/{runId:guid}/approve", async (Guid runId, ApproveBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApprovePayrollRunCommand(runId, body.Confirm, body.Exceptions, body.ConfirmEmpty, body.ConfirmWithoutSegregation), ct))
            .WithName("Payroll_Runs_Approve")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Runs.Approve");
    }

    public sealed record ApproveBody(
        bool Confirm,
        IReadOnlyList<Application.Payroll.Runs.ApprovalExceptionDto>? Exceptions,
        bool ConfirmEmpty = false,
        bool ConfirmWithoutSegregation = false);
}
