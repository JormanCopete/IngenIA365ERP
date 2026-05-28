using Carter;
using IngenIA365ERP.Application.Core.Companies.Commands.CreateCompany;
using IngenIA365ERP.Application.Core.Companies.Commands.DeleteCompany;
using IngenIA365ERP.Application.Core.Companies.Commands.UpdateCompany;
using IngenIA365ERP.Application.Core.Companies.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class CompaniesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/companies")
            .WithTags("Companies")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCompaniesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCompanies");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCompanyByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCompanyById");

        group.MapPost("/", async (CreateCompanyCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/companies/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCompany");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCompanyCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("UpdateCompany");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCompanyCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("DeleteCompany");
    }
}
