using Carter;
using IngenIA365ERP.Application.Core.Sports.Commands.CreateSport;
using IngenIA365ERP.Application.Core.Sports.Commands.UpdateSport;
using IngenIA365ERP.Application.Core.Sports.Commands.DeleteSport;
using IngenIA365ERP.Application.Core.Sports.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class SportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/sports")
            .WithTags("Sports")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSportsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSports");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSportByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSportById");

        group.MapPost("/", async (CreateSportCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/sports/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSport");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSportCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSport");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSportCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSport");
    }
}
