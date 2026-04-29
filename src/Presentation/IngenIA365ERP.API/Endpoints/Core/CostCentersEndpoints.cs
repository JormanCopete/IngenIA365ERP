using Carter;
using IngenIA365ERP.Application.Core.CostCenters.Commands.CreateCostCenter;
using IngenIA365ERP.Application.Core.CostCenters.Commands.UpdateCostCenter;
using IngenIA365ERP.Application.Core.CostCenters.Commands.DeleteCostCenter;
using IngenIA365ERP.Application.Core.CostCenters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class CostCentersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/cost-centers")
            .WithTags("CostCenters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCostCentersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCostCenters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCostCenterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCostCenterById");

        group.MapPost("/", async (CreateCostCenterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/cost-centers/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCostCenter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCostCenterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateCostCenter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCostCenterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteCostCenter");
    }
}
