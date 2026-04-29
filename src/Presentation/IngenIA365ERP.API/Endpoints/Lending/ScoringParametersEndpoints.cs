using Carter;
using IngenIA365ERP.Application.Lending.ScoringParameters.Commands.CreateScoringParameter;
using IngenIA365ERP.Application.Lending.ScoringParameters.Commands.UpdateScoringParameter;
using IngenIA365ERP.Application.Lending.ScoringParameters.Commands.DeleteScoringParameter;
using IngenIA365ERP.Application.Lending.ScoringParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class ScoringParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/scoring-parameters")
            .WithTags("ScoringParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListScoringParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListScoringParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetScoringParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetScoringParameterById");

        group.MapPost("/", async (CreateScoringParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/scoring-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateScoringParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateScoringParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateScoringParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteScoringParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteScoringParameter");
    }
}
