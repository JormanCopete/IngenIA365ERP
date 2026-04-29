using Carter;
using IngenIA365ERP.Application.Lending.ProvisionParameters.Commands.CreateProvisionParameter;
using IngenIA365ERP.Application.Lending.ProvisionParameters.Commands.UpdateProvisionParameter;
using IngenIA365ERP.Application.Lending.ProvisionParameters.Commands.DeleteProvisionParameter;
using IngenIA365ERP.Application.Lending.ProvisionParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class ProvisionParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/provision-parameters")
            .WithTags("ProvisionParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListProvisionParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListProvisionParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProvisionParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetProvisionParameterById");

        group.MapPost("/", async (CreateProvisionParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/provision-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateProvisionParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProvisionParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateProvisionParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteProvisionParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteProvisionParameter");
    }
}
