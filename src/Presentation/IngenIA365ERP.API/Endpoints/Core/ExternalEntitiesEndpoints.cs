using Carter;
using IngenIA365ERP.Application.Core.ExternalEntities.Commands.CreateExternalEntity;
using IngenIA365ERP.Application.Core.ExternalEntities.Commands.UpdateExternalEntity;
using IngenIA365ERP.Application.Core.ExternalEntities.Commands.DeleteExternalEntity;
using IngenIA365ERP.Application.Core.ExternalEntities.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class ExternalEntitiesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/external-entities")
            .WithTags("ExternalEntities")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListExternalEntitiesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListExternalEntities");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetExternalEntityByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetExternalEntityById");

        group.MapPost("/", async (CreateExternalEntityCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/external-entities/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateExternalEntity");

        group.MapPut("/{id:guid}", async (Guid id, UpdateExternalEntityCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateExternalEntity");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteExternalEntityCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteExternalEntity");
    }
}
