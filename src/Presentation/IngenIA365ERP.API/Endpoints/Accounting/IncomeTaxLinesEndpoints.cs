using Carter;
using IngenIA365ERP.Application.Accounting.IncomeTaxLines.Commands.CreateIncomeTaxLine;
using IngenIA365ERP.Application.Accounting.IncomeTaxLines.Commands.UpdateIncomeTaxLine;
using IngenIA365ERP.Application.Accounting.IncomeTaxLines.Commands.DeleteIncomeTaxLine;
using IngenIA365ERP.Application.Accounting.IncomeTaxLines.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class IncomeTaxLinesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/income-tax-lines")
            .WithTags("IncomeTaxLines")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListIncomeTaxLinesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListIncomeTaxLines");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetIncomeTaxLineByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetIncomeTaxLineById");

        group.MapPost("/", async (CreateIncomeTaxLineCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/income-tax-lines/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateIncomeTaxLine");

        group.MapPut("/{id:guid}", async (Guid id, UpdateIncomeTaxLineCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateIncomeTaxLine");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteIncomeTaxLineCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteIncomeTaxLine");
    }
}
