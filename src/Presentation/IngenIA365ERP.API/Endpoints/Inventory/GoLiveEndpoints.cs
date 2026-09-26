using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Queries;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// La puesta en marcha (feature 012, T316; contracts/api.md §13.1, §13.2; FR-089, FR-091): el saldo inicial y las cifras de
/// SOLIDO. Cada ruta sólo reenvía al <see cref="ISender"/>, con el sobre de error, <c>Idempotency-Key</c> en toda escritura y su
/// <c>.RequirePermission(...)</c> (sin él, el 404 de lo inexistente).
/// <list type="bullet">
/// <item><c>/api/inventory/opening-balances</c>: la plantilla 14 (<c>template.xlsx</c> con <c>Inventory.Warehouses.View</c>,
/// <c>import</c> con <c>Inventory.OpeningBalance.Load</c>), la lista y el detalle de los documentos <c>OpeningBalance</c> del
/// alcance, y confirmar (<see cref="ConfirmInventoryDocumentCommand"/> con el grupo <c>OpeningBalance</c>; normalmente queda en
/// aprobación), descartar y anular (sólo con la bodega no activa, lo decide <c>EfectoSaldoInicial</c>). No hay alta ni edición
/// manual: el borrador nace y se reemplaza por la plantilla.</item>
/// <item><c>/api/inventory/legacy-figures</c>: la plantilla 15 y los lotes y filas importados, con
/// <c>Inventory.LegacyFigures.Import</c>.</item>
/// </list>
/// La activación de una bodega vive en <c>WarehousesEndpoints</c> (<c>/warehouses/{id}/activation</c>, §13.3). (nuevo)
/// </summary>
public class GoLiveEndpoints : ICarterModule
{
    public const string VerBodegas = "Inventory.Warehouses.View";
    public const string CargarSaldo = "Inventory.OpeningBalance.Load";
    public const string ImportarCifras = "Inventory.LegacyFigures.Import";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        SaldoInicial(app);
        CifrasDeSolido(app);
    }

    private static void SaldoInicial(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/inventory/opening-balances").WithTags("Inventory Opening Balances").RequireAuthorization();
        const DocumentClassGroup Grupo = DocumentClassGroup.OpeningBalance;

        g.MapPlantilla(VerBodegas, CargarSaldo, PlantillaDeSaldoInicial.Clave, "Inventory_OpeningBalances",
            importar: (modo, archivo, motivo, clave) => new ImportOpeningBalanceCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });

        g.MapGet("/", async (Guid? warehousePublicId, DocumentStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryDocumentsQuery(
                    new FiltrosDeDocumentos(Grupo, DocumentClass.OpeningBalance, null, status, null, null, warehousePublicId, null, null, null),
                    new PageRequest(page ?? 1, pageSize ?? 50)), ct))
            .WithName("Inventory_OpeningBalances_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(CargarSaldo);

        g.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetInventoryDocumentQuery(id, Grupo), ct))
            .WithName("Inventory_OpeningBalances_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(CargarSaldo);

        g.MapPost("/{id:guid}/confirm", async (Guid id, CicloDeDocumentoRutas.ConfirmarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ConfirmInventoryDocumentCommand(id, Grupo)
                {
                    RowVersion = body?.RowVersion,
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_OpeningBalances_Confirm").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(CargarSaldo);

        g.MapPost("/{id:guid}/discard", async (Guid id, CicloDeDocumentoRutas.MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardInventoryDraftCommand(id, Grupo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_OpeningBalances_Discard").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(CargarSaldo);

        g.MapPost("/{id:guid}/void", async (Guid id, CicloDeDocumentoRutas.MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new VoidInventoryDocumentCommand(id, Grupo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{http.Request.Path.Value}", result.Value) : result;
            })
            .WithName("Inventory_OpeningBalances_Void").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(CargarSaldo);
    }

    private static void CifrasDeSolido(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/inventory/legacy-figures").WithTags("Inventory Legacy Figures").RequireAuthorization();

        g.MapPlantilla(VerBodegas, ImportarCifras, PlantillaDeCifrasDeSolido.Clave, "Inventory_LegacyFigures",
            importar: (modo, archivo, motivo, clave) => new ImportLegacyFiguresCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });

        g.MapGet("/", async (DateOnly? asOf, string? warehouseCode, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLegacyFigureBatchesQuery(asOf, warehouseCode), ct))
            .WithName("Inventory_LegacyFigures_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ImportarCifras);

        g.MapGet("/{batchId:guid}/rows", async (Guid batchId, bool? unresolvedOnly, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLegacyFigureRowsQuery(batchId, unresolvedOnly ?? false, page ?? 1, pageSize ?? 50), ct))
            .WithName("Inventory_LegacyFigures_Rows").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ImportarCifras);
    }
}
