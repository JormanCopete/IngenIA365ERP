using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.OpeningBalances;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 010 (contracts/api.md §4): saldos iniciales de prestaciones por empleado.</summary>
public sealed class BenefitBalancesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/benefit-balances")
            .WithTags("PayrollBenefitBalances")
            .RequireAuthorization();

        group.MapGet("/", async (DateOnly? asOf, bool? onlyMissing, string? search, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListBenefitBalancesQuery(asOf, onlyMissing ?? false, search), ct))
            .WithName("Payroll_BenefitBalances_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.BenefitBalances.View");

        group.MapGet("/{employeeId:guid}", async (Guid employeeId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetBenefitBalanceQuery(employeeId), ct))
            .WithName("Payroll_BenefitBalances_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.BenefitBalances.View");

        group.MapPut("/{employeeId:guid}", async (Guid employeeId, UpsertBenefitBalanceCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command with { EmployeePublicId = employeeId }, ct);
                return result.IsSuccess ? Results.Ok(new { publicId = result.Value }) : (object)result;
            })
            .WithName("Payroll_BenefitBalances_Upsert")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.BenefitBalances.Manage");

        group.MapPost("/{employeeId:guid}/adjustments", async (Guid employeeId, AddBenefitBalanceAdjustmentCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command with { EmployeePublicId = employeeId }, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/benefit-balances/{employeeId}", new { publicId = result.Value })
                    : (object)result;
            })
            .WithName("Payroll_BenefitBalances_AddAdjustment")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.BenefitBalances.Manage");
    }
}
