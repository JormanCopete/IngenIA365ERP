using Carter;
using IngenIA365ERP.Application.Inventory.Documents.Commands.CreateInventoryDocument;
using IngenIA365ERP.Application.Inventory.Documents.Commands.VoidInventoryDocument;
using IngenIA365ERP.Application.Inventory.Documents.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class InventoryDocumentsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/documents")
            .WithTags("InventoryDocuments")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListInventoryDocumentsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListInventoryDocuments");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetInventoryDocumentByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetInventoryDocumentById");

        group.MapPost("/", async (CreateInventoryDocumentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/documents/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateInventoryDocument");

        group.MapPost("/{id:guid}/void", async (Guid id, VoidInventoryDocumentRequest? request, ISender sender) =>
        {
            var command = new VoidInventoryDocumentCommand(id, request?.Reason);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("VoidInventoryDocument");

        // Stock endpoints
        group.MapGet("/stock/{productId:guid}", async (Guid productId, ISender sender) =>
        {
            var result = await sender.Send(new GetProductStockQuery(productId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetProductStock");

        group.MapGet("/stock/{productId:guid}/movements",
            async (Guid productId, [AsParameters] StockMovementsFilter filter, ISender sender) =>
        {
            var query = new GetStockMovementsQuery
            {
                ProductPublicId = productId,
                WarehousePublicId = filter.WarehouseId,
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo
            };
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetStockMovements");
    }
}

public record VoidInventoryDocumentRequest(string? Reason);

public record StockMovementsFilter
{
    public Guid? WarehouseId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}
