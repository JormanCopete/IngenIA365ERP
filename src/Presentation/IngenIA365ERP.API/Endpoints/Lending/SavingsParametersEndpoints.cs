using Carter;
using IngenIA365ERP.Application.Lending.SavingsParameters.Commands.CreateSavingsParameter;
using IngenIA365ERP.Application.Lending.SavingsParameters.Commands.UpdateSavingsParameter;
using IngenIA365ERP.Application.Lending.SavingsParameters.Commands.DeleteSavingsParameter;
using IngenIA365ERP.Application.Lending.SavingsParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class SavingsParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/savings-parameters")
            .WithTags("SavingsParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSavingsParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSavingsParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSavingsParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSavingsParameterById");

        group.MapPost("/", async (CreateSavingsParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/savings-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSavingsParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSavingsParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSavingsParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSavingsParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSavingsParameter");
    }
}
