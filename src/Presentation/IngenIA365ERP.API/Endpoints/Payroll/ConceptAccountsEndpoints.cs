using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.ConceptAccounts.Commands.CreateConceptAccount;
using IngenIA365ERP.Application.Payroll.ConceptAccounts.Commands.UpdateConceptAccount;
using IngenIA365ERP.Application.Payroll.ConceptAccounts.Commands.DeleteConceptAccount;
using IngenIA365ERP.Application.Payroll.ConceptAccounts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class ConceptAccountsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/concept-accounts")
            .WithTags("ConceptAccounts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListConceptAccountsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListConceptAccounts")
            .RequirePermission(new AlgunPermiso("Payroll.Entities.View", "Payroll.Employees.View", "Payroll.Concepts.View"));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetConceptAccountByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetConceptAccountById")
            .RequirePermission(new AlgunPermiso("Payroll.Entities.View", "Payroll.Employees.View", "Payroll.Concepts.View"));

        group.MapPost("/", async (CreateConceptAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/concept-accounts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateConceptAccount")
            .RequirePermission("Payroll.Concepts.Manage");

        group.MapPut("/{id:guid}", async (Guid id, UpdateConceptAccountCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateConceptAccount")
            .RequirePermission("Payroll.Concepts.Manage");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteConceptAccountCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteConceptAccount")
            .RequirePermission("Payroll.Concepts.Manage");
    }
}
