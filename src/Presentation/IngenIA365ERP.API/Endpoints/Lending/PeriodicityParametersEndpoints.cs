using Carter;
using IngenIA365ERP.Application.Lending.PeriodicityParameters.Commands.CreatePeriodicityParameter;
using IngenIA365ERP.Application.Lending.PeriodicityParameters.Commands.UpdatePeriodicityParameter;
using IngenIA365ERP.Application.Lending.PeriodicityParameters.Commands.DeletePeriodicityParameter;
using IngenIA365ERP.Application.Lending.PeriodicityParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class PeriodicityParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/periodicity-parameters")
            .WithTags("PeriodicityParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPeriodicityParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPeriodicityParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPeriodicityParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPeriodicityParameterById");

        group.MapPost("/", async (CreatePeriodicityParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/periodicity-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePeriodicityParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePeriodicityParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePeriodicityParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePeriodicityParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePeriodicityParameter");
    }
}
