using Carter;
using IngenIA365ERP.Application.Inventory.Documents.Commands.CreateInvoice;
using IngenIA365ERP.Application.Inventory.Documents.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class InvoicesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/invoices")
            .WithTags("InventoryInvoices")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListInvoicesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListInvoices");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetInvoiceByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetInvoiceById");

        group.MapPost("/", async (CreateInvoiceCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/invoices/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateInvoice");
    }
}
