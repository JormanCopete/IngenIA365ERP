using Carter;
using IngenIA365ERP.Application.Inventory.SecondaryGroups.Commands.CreateSecondaryGroup;
using IngenIA365ERP.Application.Inventory.SecondaryGroups.Commands.UpdateSecondaryGroup;
using IngenIA365ERP.Application.Inventory.SecondaryGroups.Commands.DeleteSecondaryGroup;
using IngenIA365ERP.Application.Inventory.SecondaryGroups.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class SecondaryGroupsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/secondary-groups")
            .WithTags("SecondaryGroups")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSecondaryGroupsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSecondaryGroups");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSecondaryGroupByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSecondaryGroupById");

        group.MapPost("/", async (CreateSecondaryGroupCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/secondary-groups/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSecondaryGroup");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSecondaryGroupCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSecondaryGroup");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSecondaryGroupCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSecondaryGroup");
    }
}
