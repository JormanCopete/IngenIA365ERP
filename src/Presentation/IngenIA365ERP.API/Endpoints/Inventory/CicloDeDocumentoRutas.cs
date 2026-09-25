using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Queries;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// El ciclo común de un grupo de documentos (feature 012, T149; contracts/api.md §9.3): lo publica cada historia sobre su
/// ruta (<c>/api/inventory/adjustments</c>, <c>/transfers</c>, <c>/purchases/…</c>, <c>/opening-balances</c>…) con
/// <see cref="MapCicloDeDocumento"/>, así ajustes, traslados, compras y ventas comparten comandos, permisos,
/// idempotencia y sobre de error. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// <list type="bullet">
/// <item><c>GET /</c> y <c>GET /{id}</c> con <c>{prefijo}.View</c>;</item>
/// <item><c>POST /</c>, <c>PUT /{id}</c> y <c>POST /{id}/discard</c> con <c>{prefijo}.Create</c>;</item>
/// <item><c>POST /{id}/confirm</c> con <c>{prefijo}.Confirm</c> y <c>POST /{id}/void</c> con <c>{prefijo}.Void</c>.</item>
/// </list>
/// Toda escritura exige <c>Idempotency-Key</c>. Sin permiso, el mismo 404 que lo inexistente.
/// </summary>
public static class CicloDeDocumentoRutas
{
    /// <summary>Publica el ciclo común de <paramref name="grupoDeDocumentos"/> sobre <paramref name="grupo"/>.</summary>
    /// <param name="grupo">El grupo de rutas de la historia (ya con <c>RequireAuthorization()</c>).</param>
    /// <param name="grupoDeDocumentos">El <see cref="DocumentClassGroup"/> que se compara con la clase de cada documento.</param>
    /// <param name="prefijoDePermiso"><c>Inventory.Adjustments</c>, <c>Inventory.Purchases</c>…</param>
    /// <param name="nombre">Prefijo de los nombres de ruta (<c>Inventory_Adjustments</c>).</param>
    public static RouteGroupBuilder MapCicloDeDocumento(
        this RouteGroupBuilder grupo, DocumentClassGroup grupoDeDocumentos, string prefijoDePermiso, string nombre)
    {
        var ver = $"{prefijoDePermiso}.View";
        var crear = $"{prefijoDePermiso}.Create";
        var confirmar = $"{prefijoDePermiso}.Confirm";
        var anular = $"{prefijoDePermiso}.Void";

        grupo.MapGet("/", async (
                DocumentClass? @class, Guid? documentTypePublicId, DocumentStatus? status, DateOnly? from, DateOnly? to,
                Guid? warehousePublicId, Guid? counterpartyPersonPublicId, string? number, string? search, int? page, int? pageSize,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryDocumentsQuery(
                    new FiltrosDeDocumentos(grupoDeDocumentos, @class, documentTypePublicId, status, from, to, warehousePublicId,
                        counterpartyPersonPublicId, number, search),
                    new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName($"{nombre}_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(ver);

        grupo.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetInventoryDocumentQuery(id, grupoDeDocumentos), ct))
            .WithName($"{nombre}_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(ver);

        grupo.MapPost("/", async (SaveInventoryDraftRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveInventoryDraftCommand(null, grupoDeDocumentos, body) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{http.Request.Path.Value?.TrimEnd('/')}/{result.Value.PublicId}", result.Value) : result;
            })
            .WithName($"{nombre}_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(crear);

        grupo.MapPut("/{id:guid}", async (Guid id, SaveInventoryDraftRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveInventoryDraftCommand(id, grupoDeDocumentos, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName($"{nombre}_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(crear);

        grupo.MapPost("/{id:guid}/discard", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardInventoryDraftCommand(id, grupoDeDocumentos, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName($"{nombre}_Discard")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(crear);

        grupo.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ConfirmInventoryDocumentCommand(id, grupoDeDocumentos)
                {
                    RowVersion = body?.RowVersion,
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName($"{nombre}_Confirm")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(confirmar);

        grupo.MapPost("/{id:guid}/void", async (Guid id, AnularRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new VoidInventoryDocumentCommand(id, grupoDeDocumentos, body.Reason ?? string.Empty, body.OperationDate)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"{http.Request.Path.Value}", result.Value) : result;
            })
            .WithName($"{nombre}_Void")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(anular);

        return grupo;
    }

    /// <summary>El cuerpo de descartar (§9.3).</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El cuerpo de confirmar (§9.3): la versión leída del borrador.</summary>
    public sealed record ConfirmarRequest(byte[]? RowVersion);

    /// <summary>El cuerpo de anular (§9.5): motivo y, si el tipo lo admite, otra fecha que hoy.</summary>
    public sealed record AnularRequest(string? Reason, DateOnly? OperationDate);
}
