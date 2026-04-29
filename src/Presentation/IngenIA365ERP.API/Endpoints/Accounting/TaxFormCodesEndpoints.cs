using Carter;
using IngenIA365ERP.Application.Accounting.TaxFormCodes.Commands.CreateTaxFormCode;
using IngenIA365ERP.Application.Accounting.TaxFormCodes.Commands.UpdateTaxFormCode;
using IngenIA365ERP.Application.Accounting.TaxFormCodes.Commands.DeleteTaxFormCode;
using IngenIA365ERP.Application.Accounting.TaxFormCodes.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class TaxFormCodesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/tax-form-codes")
            .WithTags("TaxFormCodes")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListTaxFormCodesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListTaxFormCodes");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetTaxFormCodeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetTaxFormCodeById");

        group.MapPost("/", async (CreateTaxFormCodeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/tax-form-codes/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateTaxFormCode");

        group.MapPut("/{id:guid}", async (Guid id, UpdateTaxFormCodeCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateTaxFormCode");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteTaxFormCodeCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteTaxFormCode");
    }
}
