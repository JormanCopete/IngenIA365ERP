using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Concepts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 005 (contracts/api.md §6): definiciones de concepto con vigencia. Los comandos de US3 se añaden aquí.</summary>
public sealed class PayrollConceptDefinitionsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/concept-definitions")
            .WithTags("PayrollConcepts")
            .RequireAuthorization();

        group.MapGet("/", async (DateTime? asOf, bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListConceptDefinitionsQuery(asOf, includeInactive ?? false), ct))
            .WithName("Payroll_Concepts_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.View");

        group.MapGet("/{code}/versions", async (string code, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListConceptVersionsQuery(code), ct))
            .WithName("Payroll_Concepts_Versions")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.View");
    }
}
