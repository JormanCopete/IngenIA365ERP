using Carter;
using IngenIA365ERP.Application.Inventory.CommissionParameters.Commands.CreateCommissionParameter;
using IngenIA365ERP.Application.Inventory.CommissionParameters.Commands.UpdateCommissionParameter;
using IngenIA365ERP.Application.Inventory.CommissionParameters.Commands.DeleteCommissionParameter;
using IngenIA365ERP.Application.Inventory.CommissionParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class CommissionParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/commission-parameters")
            .WithTags("CommissionParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCommissionParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCommissionParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCommissionParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCommissionParameterById");

        group.MapPost("/", async (CreateCommissionParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/commission-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCommissionParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCommissionParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateCommissionParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCommissionParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteCommissionParameter");
    }
}
