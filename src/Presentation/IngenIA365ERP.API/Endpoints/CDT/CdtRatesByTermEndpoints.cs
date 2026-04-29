using Carter;
using IngenIA365ERP.Application.CDT.CdtRatesByTerm.Commands.CreateCdtRateByTerm;
using IngenIA365ERP.Application.CDT.CdtRatesByTerm.Commands.UpdateCdtRateByTerm;
using IngenIA365ERP.Application.CDT.CdtRatesByTerm.Commands.DeleteCdtRateByTerm;
using IngenIA365ERP.Application.CDT.CdtRatesByTerm.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.CDT;

public class CdtRatesByTermEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cdt/rates-by-term")
            .WithTags("CdtRatesByTerm")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCdtRatesByTermQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCdtRatesByTerm");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCdtRateByTermByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCdtRateByTermById");

        group.MapPost("/", async (CreateCdtRateByTermCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/cdt/rates-by-term/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCdtRateByTerm");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCdtRateByTermCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateCdtRateByTerm");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCdtRateByTermCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteCdtRateByTerm");
    }
}
