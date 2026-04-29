using Carter;
using IngenIA365ERP.Application.Accounting.RiskCategories.Commands.CreateRiskCategory;
using IngenIA365ERP.Application.Accounting.RiskCategories.Commands.UpdateRiskCategory;
using IngenIA365ERP.Application.Accounting.RiskCategories.Commands.DeleteRiskCategory;
using IngenIA365ERP.Application.Accounting.RiskCategories.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class RiskCategoriesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/risk-categories")
            .WithTags("RiskCategories")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListRiskCategoriesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListRiskCategories");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetRiskCategoryByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetRiskCategoryById");

        group.MapPost("/", async (CreateRiskCategoryCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/risk-categories/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateRiskCategory");

        group.MapPut("/{id:guid}", async (Guid id, UpdateRiskCategoryCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateRiskCategory");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteRiskCategoryCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteRiskCategory");
    }
}
