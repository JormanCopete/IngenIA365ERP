using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents.Queries;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// La lista y el detalle genéricos de documentos de inventario (feature 012, T149; contracts/api.md §9.2), sin
/// escritura: cada grupo escribe por su propia ruta con <see cref="CicloDeDocumentoRutas.MapCicloDeDocumento"/>. Con
/// <c>Inventory.Documents.View</c>; la consulta además filtra por el <c>View</c> de cada grupo y por el alcance. (nuevo)
/// </summary>
public class DocumentsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/documents")
            .WithTags("Inventory Documents")
            .RequireAuthorization();

        group.MapGet("/", async (
                [Microsoft.AspNetCore.Mvc.FromQuery(Name = "group")] DocumentClassGroup? grupo, DocumentClass? @class, Guid? documentTypePublicId, DocumentStatus? status, DateOnly? from, DateOnly? to,
                Guid? warehousePublicId, Guid? counterpartyPersonPublicId, string? number, string? search, int? page, int? pageSize,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryDocumentsQuery(
                    new FiltrosDeDocumentos(grupo, @class, documentTypePublicId, status, from, to, warehousePublicId, counterpartyPersonPublicId, number, search),
                    new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_Documents_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Documents.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetInventoryDocumentQuery(id), ct))
            .WithName("Inventory_Documents_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Documents.View");
    }
}
