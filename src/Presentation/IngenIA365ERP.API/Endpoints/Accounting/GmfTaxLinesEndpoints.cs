using Carter;
using IngenIA365ERP.Application.Accounting.GmfTaxLines.Commands.CreateGmfTaxLine;
using IngenIA365ERP.Application.Accounting.GmfTaxLines.Commands.UpdateGmfTaxLine;
using IngenIA365ERP.Application.Accounting.GmfTaxLines.Commands.DeleteGmfTaxLine;
using IngenIA365ERP.Application.Accounting.GmfTaxLines.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class GmfTaxLinesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/gmf-tax-lines")
            .WithTags("GmfTaxLines")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListGmfTaxLinesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListGmfTaxLines");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetGmfTaxLineByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetGmfTaxLineById");

        group.MapPost("/", async (CreateGmfTaxLineCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/gmf-tax-lines/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateGmfTaxLine");

        group.MapPut("/{id:guid}", async (Guid id, UpdateGmfTaxLineCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateGmfTaxLine");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteGmfTaxLineCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteGmfTaxLine");
    }
}
