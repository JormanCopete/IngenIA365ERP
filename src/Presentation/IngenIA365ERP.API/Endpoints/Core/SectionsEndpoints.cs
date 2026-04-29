using Carter;
using IngenIA365ERP.Application.Core.Sections.Commands.CreateSection;
using IngenIA365ERP.Application.Core.Sections.Commands.UpdateSection;
using IngenIA365ERP.Application.Core.Sections.Commands.DeleteSection;
using IngenIA365ERP.Application.Core.Sections.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class SectionsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/sections")
            .WithTags("Sections")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSectionsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSections");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSectionByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSectionById");

        group.MapPost("/", async (CreateSectionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/sections/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSection");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSectionCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSection");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSectionCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSection");
    }
}
