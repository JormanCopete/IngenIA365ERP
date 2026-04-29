using Carter;
using IngenIA365ERP.Application.Inventory.PrimaryGroups.Commands.CreatePrimaryGroup;
using IngenIA365ERP.Application.Inventory.PrimaryGroups.Commands.UpdatePrimaryGroup;
using IngenIA365ERP.Application.Inventory.PrimaryGroups.Commands.DeletePrimaryGroup;
using IngenIA365ERP.Application.Inventory.PrimaryGroups.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class PrimaryGroupsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/primary-groups")
            .WithTags("PrimaryGroups")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPrimaryGroupsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPrimaryGroups");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPrimaryGroupByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPrimaryGroupById");

        group.MapPost("/", async (CreatePrimaryGroupCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/primary-groups/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePrimaryGroup");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePrimaryGroupCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePrimaryGroup");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePrimaryGroupCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePrimaryGroup");
    }
}
