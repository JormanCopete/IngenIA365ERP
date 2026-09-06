using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Plans;
using IngenIA365ERP.Application.Payroll.Plans.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 005 (contracts/api.md §1): planes de nómina y cambio de plan del empleado.</summary>
public sealed class PayrollPlansEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll")
            .WithTags("PayrollPlans")
            .RequireAuthorization();

        group.MapGet("/plans", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPayrollPlansQuery(includeInactive ?? false), ct))
            .WithName("Payroll_Plans_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Plans.View");

        group.MapPost("/plans", async (CreatePayrollPlanCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/plans/{result.Value}", new { publicId = result.Value })
                    : (object)result;
            })
            .WithName("Payroll_Plans_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Plans.Manage");

        group.MapPut("/plans/{planId:guid}", async (Guid planId, UpdatePayrollPlanCommand command, ISender sender, CancellationToken ct) =>
                await sender.Send(command with { PublicId = planId }, ct))
            .WithName("Payroll_Plans_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Plans.Manage");

        group.MapPost("/employees/{employeeId:guid}/plan", async (Guid employeeId, ChangeEmployeePlanCommand command, ISender sender, CancellationToken ct) =>
                await sender.Send(command with { EmployeePublicId = employeeId }, ct))
            .WithName("Payroll_Employees_ChangePlan")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Plans.Manage");
    }
}
