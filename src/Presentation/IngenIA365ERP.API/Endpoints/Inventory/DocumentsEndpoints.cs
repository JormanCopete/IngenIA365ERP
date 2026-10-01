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

        // Feature 012, I2 (T529; api.md §25.1): la validación previa del documento tal como está. Es una consulta: sin clave de
        // operación, no guarda nada ni toma el cerrojo. El cuerpo ({ payments? }) lo usan las ventas de I3; hoy se ignora.
        group.MapPost("/{id:guid}/prevalidate", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new IngenIA365ERP.Application.Inventory.Integration.PrevalidateInventoryDocumentQuery(id), ct))
            .WithName("Inventory_Documents_Prevalidate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Documents.View");

        // Feature 012, I5 (T845; api.md §2.3, §9.3; FR-045): el impacto en costos de un borrador retroactivo antes de confirmarlo. Es una
        // consulta: sin cuerpo, sin clave de operación y sin guardar nada. La ruta exige Inventory.Costs.Read; el {Grupo}.Create del
        // grupo del documento y el alcance los mira GetDocumentCostImpactQuery (sin ellos, el mismo 404 genérico del documento).
        group.MapPost("/{id:guid}/cost-impact", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new IngenIA365ERP.Application.Inventory.Costing.GetDocumentCostImpactQuery(id), ct))
            .WithName("Inventory_Documents_CostImpact")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(IngenIA365ERP.Application.Inventory.Documents.PermisosDeGrupo.LeerCostos);

        // Feature 012, I3 (T632; api.md §20.3): la reimpresión, con la marca «COPIA», de la copia fiscal vigente. Sirve para cualquier
        // grupo; las ventas usan tirilla o carta. Queda auditada (ReprintDocumentCommand) y lleva clave de operación.
        group.MapPost("/{id:guid}/reprint", async (Guid id, SalesEndpoints.EntregaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                SalesEndpoints.ComoArchivoOJson(http, await sender.Send(new IngenIA365ERP.Application.Inventory.Sales.ReprintDocumentCommand(
                    id, body.Format ?? IngenIA365ERP.Application.Inventory.Pos.CashRegisterPrintFormat.Ticket80, body.Reason)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct)))
            .WithName("Inventory_Documents_Reprint")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.Documents.Reprint");
    }
}
