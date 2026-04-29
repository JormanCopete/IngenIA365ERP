using Carter;
using IngenIA365ERP.Application.Core.Relationships.Commands.CreateRelationship;
using IngenIA365ERP.Application.Core.Relationships.Commands.UpdateRelationship;
using IngenIA365ERP.Application.Core.Relationships.Commands.DeleteRelationship;
using IngenIA365ERP.Application.Core.Relationships.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class RelationshipsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/relationships")
            .WithTags("Relationships")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListRelationshipsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListRelationships");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetRelationshipByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetRelationshipById");

        group.MapPost("/", async (CreateRelationshipCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/relationships/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateRelationship");

        group.MapPut("/{id:guid}", async (Guid id, UpdateRelationshipCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateRelationship");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteRelationshipCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteRelationship");
    }
}
