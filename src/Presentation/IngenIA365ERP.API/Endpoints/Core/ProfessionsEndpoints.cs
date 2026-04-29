using Carter;
using IngenIA365ERP.Application.Core.Professions.Commands.CreateProfession;
using IngenIA365ERP.Application.Core.Professions.Commands.UpdateProfession;
using IngenIA365ERP.Application.Core.Professions.Commands.DeleteProfession;
using IngenIA365ERP.Application.Core.Professions.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class ProfessionsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/professions")
            .WithTags("Professions")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListProfessionsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListProfessions");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProfessionByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetProfessionById");

        group.MapPost("/", async (CreateProfessionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/professions/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateProfession");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProfessionCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateProfession");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteProfessionCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteProfession");
    }
}
