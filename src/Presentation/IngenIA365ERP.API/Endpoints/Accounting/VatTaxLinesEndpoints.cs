using Carter;
using IngenIA365ERP.Application.Accounting.VatTaxLines.Commands.CreateVatTaxLine;
using IngenIA365ERP.Application.Accounting.VatTaxLines.Commands.UpdateVatTaxLine;
using IngenIA365ERP.Application.Accounting.VatTaxLines.Commands.DeleteVatTaxLine;
using IngenIA365ERP.Application.Accounting.VatTaxLines.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class VatTaxLinesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/vat-tax-lines")
            .WithTags("VatTaxLines")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListVatTaxLinesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListVatTaxLines");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetVatTaxLineByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetVatTaxLineById");

        group.MapPost("/", async (CreateVatTaxLineCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/vat-tax-lines/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateVatTaxLine");

        group.MapPut("/{id:guid}", async (Guid id, UpdateVatTaxLineCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateVatTaxLine");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteVatTaxLineCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteVatTaxLine");
    }
}
