using Carter;
using IngenIA365ERP.Application.Core.Countries.Commands.CreateCountry;
using IngenIA365ERP.Application.Core.Countries.Commands.DeleteCountry;
using IngenIA365ERP.Application.Core.Countries.Commands.UpdateCountry;
using IngenIA365ERP.Application.Core.Countries.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class CountriesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/countries")
            .WithTags("Countries")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCountriesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCountries");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCountryByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCountryById");

        group.MapPost("/", async (CreateCountryCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/countries/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCountry");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCountryCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("UpdateCountry");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCountryCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("DeleteCountry");
    }
}
