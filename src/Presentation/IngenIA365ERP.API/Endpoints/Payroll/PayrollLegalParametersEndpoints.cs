using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.LegalParameters;
using IngenIA365ERP.Application.Payroll.LegalParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 005 (contracts/api.md §6): parámetros legales con vigencia y sus tablas por rangos.</summary>
public sealed class PayrollLegalParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/legal-parameters")
            .WithTags("PayrollLegalParameters")
            .RequireAuthorization();

        group.MapGet("/", async (DateTime? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLegalParametersQuery(asOf), ct))
            .WithName("Payroll_LegalParameters_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.LegalParameters.View");

        group.MapGet("/{code}/versions", async (string code, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLegalParameterVersionsQuery(code), ct))
            .WithName("Payroll_LegalParameters_Versions")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.LegalParameters.View");

        group.MapPost("/{code}/versions", async (string code, AddLegalParameterVersionCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command with { Code = code }, ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/legal-parameters/{code}/versions", new { publicId = result.Value }) : (object)result;
            })
            .WithName("Payroll_LegalParameters_AddVersion")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.LegalParameters.Manage");
    }
}
