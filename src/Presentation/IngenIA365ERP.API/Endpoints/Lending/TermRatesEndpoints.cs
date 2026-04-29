using Carter;
using IngenIA365ERP.Application.Lending.TermRates.Commands.CreateTermRate;
using IngenIA365ERP.Application.Lending.TermRates.Commands.UpdateTermRate;
using IngenIA365ERP.Application.Lending.TermRates.Commands.DeleteTermRate;
using IngenIA365ERP.Application.Lending.TermRates.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class TermRatesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/term-rates")
            .WithTags("TermRates")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListTermRatesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListTermRates");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetTermRateByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetTermRateById");

        group.MapPost("/", async (CreateTermRateCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/term-rates/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateTermRate");

        group.MapPut("/{id:guid}", async (Guid id, UpdateTermRateCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateTermRate");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteTermRateCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteTermRate");
    }
}
