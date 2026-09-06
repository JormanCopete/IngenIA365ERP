using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Concepts;
using IngenIA365ERP.Application.Payroll.Concepts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 005 (contracts/api.md §6): definiciones de concepto con vigencia, cuentas, catálogo heredado, prueba en seco y semilla.</summary>
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

        group.MapGet("/legacy", async (string? search, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLegacyConceptsQuery(search), ct))
            .WithName("Payroll_Concepts_Legacy")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.View");

        group.MapGet("/{code}/versions", async (string code, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListConceptVersionsQuery(code), ct))
            .WithName("Payroll_Concepts_Versions")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.View");

        group.MapPost("/", async (ConceptDefinitionInput definition, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateConceptDefinitionCommand(definition), ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/concept-definitions/{definition.Code}/versions", new { publicId = result.Value }) : (object)result;
            })
            .WithName("Payroll_Concepts_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.Manage");

        group.MapPut("/{code}", async (string code, ConceptDefinitionInput definition, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ReviseConceptDefinitionCommand(code, definition), ct);
                return result.IsSuccess ? Results.Ok(new { publicId = result.Value }) : (object)result;
            })
            .WithName("Payroll_Concepts_Revise")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.Manage");

        group.MapPost("/{code}/deactivate", async (string code, DeactivateBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeactivateConceptDefinitionCommand(code, body.ValidTo), ct))
            .WithName("Payroll_Concepts_Deactivate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.Manage");

        group.MapPut("/{code}/accounts", async (string code, AccountsBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetConceptAccountsCommand(code, body.Rows), ct))
            .WithName("Payroll_Concepts_Accounts")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.Manage");

        group.MapPost("/dry-run", async (DryRunConceptQuery query, ISender sender, CancellationToken ct) =>
                await sender.Send(query, ct))
            .WithName("Payroll_Concepts_DryRun")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.View");

        group.MapPost("/seed", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new ReapplyConceptSeedCommand(), ct))
            .WithName("Payroll_Concepts_Seed")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Concepts.Manage");
    }

    public sealed record DeactivateBody(DateTime ValidTo);
    public sealed record AccountsBody(IReadOnlyList<ConceptAccountRow> Rows);
}
