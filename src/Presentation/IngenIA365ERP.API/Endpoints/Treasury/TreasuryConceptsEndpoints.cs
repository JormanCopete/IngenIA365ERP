using Carter;
using IngenIA365ERP.Application.Treasury.TreasuryConcepts.Commands.CreateTreasuryConcept;
using IngenIA365ERP.Application.Treasury.TreasuryConcepts.Commands.UpdateTreasuryConcept;
using IngenIA365ERP.Application.Treasury.TreasuryConcepts.Commands.DeleteTreasuryConcept;
using IngenIA365ERP.Application.Treasury.TreasuryConcepts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

public class TreasuryConceptsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/treasury/concepts")
            .WithTags("TreasuryConcepts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListTreasuryConceptsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListTreasuryConcepts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetTreasuryConceptByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetTreasuryConceptById");

        group.MapPost("/", async (CreateTreasuryConceptCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/treasury/concepts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateTreasuryConcept");

        group.MapPut("/{id:guid}", async (Guid id, UpdateTreasuryConceptCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateTreasuryConcept");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteTreasuryConceptCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteTreasuryConcept");
    }
}
