using Carter;
using IngenIA365ERP.Application.CDT.CdtParameters.Commands.CreateCdtParameter;
using IngenIA365ERP.Application.CDT.CdtParameters.Commands.UpdateCdtParameter;
using IngenIA365ERP.Application.CDT.CdtParameters.Commands.DeleteCdtParameter;
using IngenIA365ERP.Application.CDT.CdtParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.CDT;

public class CdtParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cdt/parameters")
            .WithTags("CdtParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCdtParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCdtParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCdtParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCdtParameterById");

        group.MapPost("/", async (CreateCdtParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/cdt/parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCdtParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCdtParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateCdtParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCdtParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteCdtParameter");
    }
}
