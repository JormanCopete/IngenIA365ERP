using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Counts;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Conteos físicos (feature 012, US11, T402; contracts/api.md §12): la lista con su estado derivado y el detalle con la regla del
/// conteo ciego (<c>Counts.View</c>); definir, editar la definición, abrir con la foto y descartar (<c>Counts.Open</c>); capturar por
/// tandas (<c>Counts.Capture</c>) y consultar las capturas (<c>Counts.View</c>); cerrar con su número, generar el ajuste y anular un
/// conteo cerrado (<c>Counts.Close</c>); la vista previa del ajuste (<c>Counts.View</c>). Toda escritura exige <c>Idempotency-Key</c>
/// (<c>LosComandosDeInventarioLlevanClave</c>); sin permiso o fuera del alcance, el mismo 404. (nuevo)
/// </summary>
public class CountsEndpoints : ICarterModule
{
    public const string Ruta = "/api/inventory/counts";
    private const string Ver = "Inventory.Counts.View";
    private const string Abrir = "Inventory.Counts.Open";
    private const string Capturar = "Inventory.Counts.Capture";
    private const string Cerrar = "Inventory.Counts.Close";
    private const DocumentClassGroup Grupo = DocumentClassGroup.Counts;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup(Ruta).WithTags("Inventory Counts").RequireAuthorization();

        g.MapGet("/", async (string? state, Guid? warehousePublicId, DateOnly? from, DateOnly? to, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPhysicalCountsQuery(state, warehousePublicId, from, to, new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_Counts_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetPhysicalCountQuery(id), ct))
            .WithName("Inventory_Counts_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (PhysicalCountRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreatePhysicalCountCommand(body) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/{result.Value.PublicId}", result.Value) : result;
            })
            .WithName("Inventory_Counts_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Abrir);

        g.MapPut("/{id:guid}", async (Guid id, PhysicalCountRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdatePhysicalCountCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Counts_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Abrir);

        g.MapPost("/{id:guid}/open", async (Guid id, CicloDeDocumentoRutas.ConfirmarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new OpenPhysicalCountCommand(id) { RowVersion = body?.RowVersion, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Counts_Open").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Abrir);

        g.MapPost("/{id:guid}/discard", async (Guid id, CicloDeDocumentoRutas.MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardInventoryDraftCommand(id, Grupo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Counts_Discard").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Abrir);

        g.MapPost("/{id:guid}/captures", async (Guid id, CapturarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CapturePhysicalCountCommand(id, body.Round, body.Reads ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Counts_Capture").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Capturar);

        g.MapGet("/{id:guid}/captures", async (Guid id, Guid? counterUserPublicId, Guid? productPublicId, int? page, int? pageSize, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new ListCountCapturesQuery(id, counterUserPublicId, productPublicId, new PageRequest(page ?? 1, pageSize ?? 50)), ct))
            .WithName("Inventory_Counts_Captures").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/{id:guid}/close", async (Guid id, CicloDeDocumentoRutas.ConfirmarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ClosePhysicalCountCommand(id) { RowVersion = body?.RowVersion, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Counts_Close").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Cerrar);

        g.MapGet("/{id:guid}/adjustment", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetCountAdjustmentPreviewQuery(id), ct))
            .WithName("Inventory_Counts_Adjustment_Preview").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/{id:guid}/adjustment", async (Guid id, AjusteRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GenerateCountAdjustmentCommand(id, body?.Notes) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/{id}/adjustment", result.Value) : result;
            })
            .WithName("Inventory_Counts_Adjustment_Generate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Cerrar);

        g.MapPost("/{id:guid}/void", async (Guid id, CicloDeDocumentoRutas.MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new VoidInventoryDocumentCommand(id, Grupo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/{id}", result.Value) : result;
            })
            .WithName("Inventory_Counts_Void").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Cerrar);
    }

    /// <summary>El cuerpo de <c>POST /{id}/captures</c> (§12).</summary>
    public sealed record CapturarRequest(byte Round, IReadOnlyList<CountReadRequest>? Reads);

    /// <summary>El cuerpo de <c>POST /{id}/adjustment</c> (§12).</summary>
    public sealed record AjusteRequest(string? Notes);
}
