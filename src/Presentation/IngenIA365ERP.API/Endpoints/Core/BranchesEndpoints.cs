using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.Branches.Commands.CreateBranch;
using IngenIA365ERP.Application.Core.Branches.Commands.UpdateBranch;
using IngenIA365ERP.Application.Core.Branches.Commands.DeleteBranch;
using IngenIA365ERP.Application.Core.Branches.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class BranchesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/branches")
            .WithTags("Branches")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListBranchesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListBranches");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetBranchByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetBranchById");

        // Feature 012 (T178): los fallos van con el sobre canónico y su estado (Branch.MunicipalityUnknown → 422,
        // lo no encontrado → 404). Antes el PUT respondía 404 a cualquier fallo y el POST 400 sin sobre.
        group.MapPost("/", async (CreateBranchCommand command, ISender sender, HttpContext http) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? (object?)Results.Created($"/api/core/branches/{result.Value}", result.Value)
                : ErrorEnvelopeFilter.Translate(http, result);
        }).WithName("CreateBranch");

        group.MapPut("/{id:guid}", async (Guid id, UpdateBranchCommand command, ISender sender, HttpContext http) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? (object?)Results.NoContent() : ErrorEnvelopeFilter.Translate(http, result);
        }).WithName("UpdateBranch");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteBranchCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteBranch");
    }
}
