using Carter;
using IngenIA365ERP.Application.Accounting.WithholdingTaxLines.Commands.CreateWithholdingTaxLine;
using IngenIA365ERP.Application.Accounting.WithholdingTaxLines.Commands.UpdateWithholdingTaxLine;
using IngenIA365ERP.Application.Accounting.WithholdingTaxLines.Commands.DeleteWithholdingTaxLine;
using IngenIA365ERP.Application.Accounting.WithholdingTaxLines.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class WithholdingTaxLinesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/withholding-tax-lines")
            .WithTags("WithholdingTaxLines")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListWithholdingTaxLinesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWithholdingTaxLines");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWithholdingTaxLineByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWithholdingTaxLineById");

        group.MapPost("/", async (CreateWithholdingTaxLineCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/withholding-tax-lines/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateWithholdingTaxLine");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWithholdingTaxLineCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateWithholdingTaxLine");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteWithholdingTaxLineCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteWithholdingTaxLine");
    }
}
