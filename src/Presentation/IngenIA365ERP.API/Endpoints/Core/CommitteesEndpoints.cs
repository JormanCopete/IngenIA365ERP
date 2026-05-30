using Carter;
using IngenIA365ERP.Application.Core.Committees.Commands.CreateCommittee;
using IngenIA365ERP.Application.Core.Committees.Commands.DeleteCommittee;
using IngenIA365ERP.Application.Core.Committees.Commands.UpdateCommittee;
using IngenIA365ERP.Application.Core.Committees.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class CommitteesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/committees")
            .WithTags("Committees")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCommitteesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCommittees");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCommitteeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCommitteeById");

        group.MapPost("/", async (CreateCommitteeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/committees/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCommittee");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCommitteeCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("UpdateCommittee");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCommitteeCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("DeleteCommittee");
    }
}
