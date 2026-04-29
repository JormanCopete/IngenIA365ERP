using Carter;
using IngenIA365ERP.Application.Lending.SiplaParameters.Commands.CreateSiplaParameter;
using IngenIA365ERP.Application.Lending.SiplaParameters.Commands.UpdateSiplaParameter;
using IngenIA365ERP.Application.Lending.SiplaParameters.Commands.DeleteSiplaParameter;
using IngenIA365ERP.Application.Lending.SiplaParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class SiplaParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/sipla-parameters")
            .WithTags("SiplaParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSiplaParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSiplaParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSiplaParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSiplaParameterById");

        group.MapPost("/", async (CreateSiplaParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/sipla-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSiplaParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSiplaParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSiplaParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSiplaParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSiplaParameter");
    }
}
