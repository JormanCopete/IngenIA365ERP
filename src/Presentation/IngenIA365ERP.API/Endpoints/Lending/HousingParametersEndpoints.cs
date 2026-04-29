using Carter;
using IngenIA365ERP.Application.Lending.HousingParameters.Commands.CreateHousingParameter;
using IngenIA365ERP.Application.Lending.HousingParameters.Commands.UpdateHousingParameter;
using IngenIA365ERP.Application.Lending.HousingParameters.Commands.DeleteHousingParameter;
using IngenIA365ERP.Application.Lending.HousingParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class HousingParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/housing-parameters")
            .WithTags("HousingParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListHousingParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListHousingParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetHousingParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetHousingParameterById");

        group.MapPost("/", async (CreateHousingParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/housing-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateHousingParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateHousingParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateHousingParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteHousingParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteHousingParameter");
    }
}
