using Carter;
using IngenIA365ERP.Application.Lending.InterestRates.Commands.CreateInterestRate;
using IngenIA365ERP.Application.Lending.InterestRates.Commands.UpdateInterestRate;
using IngenIA365ERP.Application.Lending.InterestRates.Commands.DeleteInterestRate;
using IngenIA365ERP.Application.Lending.InterestRates.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class InterestRatesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/interest-rates")
            .WithTags("InterestRates")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListInterestRatesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListInterestRates");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetInterestRateByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetInterestRateById");

        group.MapPost("/", async (CreateInterestRateCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/interest-rates/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateInterestRate");

        group.MapPut("/{id:guid}", async (Guid id, UpdateInterestRateCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateInterestRate");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteInterestRateCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteInterestRate");
    }
}
