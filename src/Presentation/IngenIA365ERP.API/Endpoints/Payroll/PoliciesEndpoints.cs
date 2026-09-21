using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Policies;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 010 (contracts/api.md §10.1): políticas por empresa con vigencia.</summary>
public sealed class PoliciesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/company-policies")
            .WithTags("PayrollCompanyPolicies")
            .RequireAuthorization();

        group.MapGet("/", async (DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCompanyPoliciesQuery(asOf), ct))
            .WithName("Payroll_CompanyPolicies_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.CompanyPolicies.View");

        group.MapGet("/{key}/versions", async (string key, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPolicyVersionsQuery(key), ct))
            .WithName("Payroll_CompanyPolicies_Versions")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.CompanyPolicies.View");

        group.MapPost("/{key}/versions", async (string key, AddPolicyVersionCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command with { Key = key }, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/company-policies/{key}/versions", result.Value)
                    : (object)result;
            })
            .WithName("Payroll_CompanyPolicies_AddVersion")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.CompanyPolicies.Manage");
    }
}
