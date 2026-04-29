using Carter;
using IngenIA365ERP.Application.Accounting.IcaTaxLines.Commands.CreateIcaTaxLine;
using IngenIA365ERP.Application.Accounting.IcaTaxLines.Commands.UpdateIcaTaxLine;
using IngenIA365ERP.Application.Accounting.IcaTaxLines.Commands.DeleteIcaTaxLine;
using IngenIA365ERP.Application.Accounting.IcaTaxLines.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class IcaTaxLinesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/ica-tax-lines")
            .WithTags("IcaTaxLines")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListIcaTaxLinesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListIcaTaxLines");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetIcaTaxLineByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetIcaTaxLineById");

        group.MapPost("/", async (CreateIcaTaxLineCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/ica-tax-lines/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateIcaTaxLine");

        group.MapPut("/{id:guid}", async (Guid id, UpdateIcaTaxLineCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateIcaTaxLine");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteIcaTaxLineCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteIcaTaxLine");
    }
}
