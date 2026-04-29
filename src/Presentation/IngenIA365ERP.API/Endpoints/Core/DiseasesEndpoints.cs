using Carter;
using IngenIA365ERP.Application.Core.Diseases.Commands.CreateDisease;
using IngenIA365ERP.Application.Core.Diseases.Commands.UpdateDisease;
using IngenIA365ERP.Application.Core.Diseases.Commands.DeleteDisease;
using IngenIA365ERP.Application.Core.Diseases.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class DiseasesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/diseases")
            .WithTags("Diseases")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListDiseasesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDiseases");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetDiseaseByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDiseaseById");

        group.MapPost("/", async (CreateDiseaseCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/diseases/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateDisease");

        group.MapPut("/{id:guid}", async (Guid id, UpdateDiseaseCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateDisease");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteDiseaseCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteDisease");
    }
}
