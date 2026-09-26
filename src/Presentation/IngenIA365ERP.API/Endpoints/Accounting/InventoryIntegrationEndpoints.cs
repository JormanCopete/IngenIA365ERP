using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Integration;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// El lado contable de la integración con Inventario (feature 012, I2, T528; contracts/api.md §26, contracts/contabilidad.md
/// §7.3; enmienda de la 009): la matriz de reglas y su plantilla, el mapeo de tipos de comprobante, la completitud, los lotes
/// de contabilización y los recibos. Grupo <c>/api/accounting/inventory</c> con <c>RequireAuthorization</c>, el sobre de
/// errores y un permiso por ruta; sin permiso, el mismo 404 que lo inexistente. Sólo reenvían al <see cref="ISender"/>.
///
/// <para>
/// La matriz y el mapeo siguen la mecánica de la 009: sin clave de operación. Llevan <c>Idempotency-Key</c> la orden de
/// lote (202) y la importación de la plantilla (su comando es idempotente, como toda importación). Los comandos de consumo
/// (<c>PostInventoryMessagesCommand</c>, el grupo resumido) y el ciclo del lote no tienen ruta: los envía sólo
/// <see cref="DespachadorDeMensajes"/> (<c>LosComandosDeConsumoNoTienenRuta</c>). (nuevo)
/// </para>
/// </summary>
public class InventoryIntegrationEndpoints : ICarterModule
{
    public const string ReglasVer = "Accounting.InventoryRules.View";
    public const string ReglasAdministrar = "Accounting.InventoryRules.Manage";
    public const string LotesVer = "Accounting.InventoryBatches.View";
    public const string LotesCorrer = "Accounting.InventoryBatches.Run";
    public const string ComprobantesVer = "Accounting.Vouchers.View";

    private const long TopeDelCuerpo = EjecutorDeImportacion.MaximoDeBytes + 256 * 1024;

    public sealed record CrearReglaRequest(
        string? Operation,
        string? Role,
        DimensionesDeLaReglaDto? Dimensions,
        Guid AccountPublicId,
        DateOnly ValidFrom,
        string? Notes,
        string? Reason,
        DateOnly? ValidTo);

    public sealed record VersionDeReglaRequest(Guid AccountPublicId, DateOnly ValidFrom, string? Notes, string? Reason);

    public sealed record DesactivarReglaRequest(DateOnly ValidTo, string? Reason);

    public sealed record MapeoRequest(
        string? Operation,
        string? InventoryDocumentTypeCode,
        Guid VoucherTypePublicId,
        Guid? CrossDocumentTypePublicId,
        string? Reason);

    public sealed record VistaPreviaRequest(
        DateOnly From,
        DateOnly To,
        IReadOnlyList<string>? DocumentTypeCodes,
        string? ScheduleKey,
        Guid? BranchPublicId);

    public sealed record OrdenDeLoteRequest(
        Guid CutoffMessagePublicId,
        DateOnly From,
        DateOnly To,
        IReadOnlyList<string>? DocumentTypeCodes,
        string? ScheduleKey,
        Guid? BranchPublicId,
        string? Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/accounting/inventory")
            .WithTags("Accounting Inventory Integration")
            .RequireAuthorization();

        Reglas(g);
        Mapeos(g);
        Lotes(g);
    }

    private static void Reglas(RouteGroupBuilder g)
    {
        g.MapGet("/rules/catalog", async (ISender sender, CancellationToken ct) => await sender.Send(new GetInventoryRulesCatalogQuery(), ct))
            .WithName("Accounting_Inventory_Rules_Catalog").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapGet("/rules", async (string? operation, string? role, DateOnly? asOf, string? accountingGroupCode, string? warehouseCode,
                    string? pointOfSaleCode, string? paymentMeansCode, string? taxRateCode, string? reasonCode, string? account, bool? onlyCurrent,
                    int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryPostingRulesQuery(operation, role, asOf, accountingGroupCode, warehouseCode, pointOfSaleCode,
                    paymentMeansCode, taxRateCode, reasonCode, account, onlyCurrent ?? false, new PageRequest(page ?? 1, pageSize ?? 50)), ct))
            .WithName("Accounting_Inventory_Rules_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapGet("/rules/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetInventoryPostingRuleQuery(id), ct))
            .WithName("Accounting_Inventory_Rules_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapGet("/rules/{id:guid}/versions", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryPostingRuleVersionsQuery(id), ct))
            .WithName("Accounting_Inventory_Rules_Versions").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapPost("/rules", async (CrearReglaRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateInventoryPostingRuleCommand(body.Operation ?? string.Empty, body.Role ?? string.Empty,
                    body.Dimensions ?? new DimensionesDeLaReglaDto(), body.AccountPublicId, body.ValidFrom, body.Notes, body.Reason ?? string.Empty,
                    body.ValidTo), ct);
                return r.IsSuccess ? (object)Results.Created($"/api/accounting/inventory/rules/{r.Value.RulePublicId}", r.Value) : r;
            })
            .WithName("Accounting_Inventory_Rules_Create").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasAdministrar);

        g.MapPost("/rules/{id:guid}/versions", async (Guid id, VersionDeReglaRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new AddInventoryPostingRuleVersionCommand(id, body.AccountPublicId, body.ValidFrom, body.Notes,
                    body.Reason ?? string.Empty), ct);
                return r.IsSuccess ? (object)Results.Created($"/api/accounting/inventory/rules/{r.Value.RulePublicId}", r.Value) : r;
            })
            .WithName("Accounting_Inventory_Rules_AddVersion").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasAdministrar);

        g.MapPost("/rules/{id:guid}/deactivate", async (Guid id, DesactivarReglaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeactivateInventoryPostingRuleCommand(id, body.ValidTo, body.Reason ?? string.Empty), ct))
            .WithName("Accounting_Inventory_Rules_Deactivate").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasAdministrar);

        // La plantilla 16 (contracts/plantillas.md §16): encabezados en la fila 1, instrucciones en otra hoja; con datos, las
        // reglas vigentes y futuras para exportar, corregir y volver a importar con el mismo libro.
        g.MapGet("/rules/template.xlsx", async ([FromQuery] bool? withData, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                const string nombre = "plantilla-16-matriz-contable-de-inventario";
                if (withData != true)
                {
                    var vacia = PlantillaDeImportacion.Xlsx(PlantillaDeMatrizDeInventario.Definicion, nombre);
                    return Results.File(vacia.Contenido, vacia.TipoContenido, vacia.NombreArchivo);
                }

                var datos = await sender.Send(new GetInventoryRulesTemplateQuery(), ct);
                if (datos.IsFailure) return ErrorEnvelopeFilter.Translate(http, datos);
                var llena = PlantillaDeImportacion.Xlsx(PlantillaDeMatrizDeInventario.Definicion, nombre + "-datos", datos.Value);
                return Results.File(llena.Contenido, llena.TipoContenido, llena.NombreArchivo);
            })
            .WithName("Accounting_Inventory_Rules_Template").RequirePermission(ReglasVer);

        g.MapPost("/rules/import", async (HttpContext http, ISender sender, CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType) return Sobre(http, ArchivosTabulares.Vacio.Code, ArchivosTabulares.Vacio.Message, StatusCodes.Status400BadRequest);
                var formulario = await http.Request.ReadFormAsync(ct);
                var archivo = formulario.Files.FirstOrDefault();
                if (archivo is null || archivo.Length == 0)
                    return Sobre(http, ArchivosTabulares.Vacio.Code, ArchivosTabulares.Vacio.Message, StatusCodes.Status400BadRequest);
                if (archivo.Length > EjecutorDeImportacion.MaximoDeBytes)
                    return Sobre(http, "Archivo.DemasiadoGrande", "El archivo pesa más de 16 MB.", StatusCodes.Status413PayloadTooLarge);

                var modo = Enum.TryParse<ModoDeImportacion>(http.Request.Query["mode"].ToString(), ignoreCase: true, out var m) && Enum.IsDefined(m)
                    ? m
                    : (ModoDeImportacion?)null;
                var motivo = formulario["reason"].ToString();
                if (motivo.Length > 400) motivo = motivo[..400];

                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms, ct);
                var subido = new ArchivoDeImportacion(archivo.FileName, ms.ToArray());
                var resultado = await sender.Send(new ImportInventoryPostingRulesCommand(modo, subido, motivo) { OperationKey = http.ClaveDeOperacion() }, ct);

                if (resultado.IsSuccess && resultado.Value.Mode == ModoDeImportacion.Review
                    && string.Equals(http.Request.Query["format"].ToString(), "xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    var revisado = PlantillaDeImportacion.ConResultados(subido, PlantillaDeMatrizDeInventario.Definicion, resultado.Value);
                    return Results.File(revisado.Contenido, revisado.TipoContenido, revisado.NombreArchivo);
                }
                return (object)resultado;
            })
            .WithName("Accounting_Inventory_Rules_Import")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(TopeDelCuerpo))
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(ReglasAdministrar);

        g.MapGet("/completeness", async (DateOnly? date, ISender sender, IngenIA365ERP.Application.Common.Interfaces.IDateTimeService reloj, CancellationToken ct) =>
                await sender.Send(new InventoryRulesCompletenessQuery(date ?? reloj.HoyLocal), ct))
            .WithName("Accounting_Inventory_Completeness").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);
    }

    private static void Mapeos(RouteGroupBuilder g)
    {
        g.MapGet("/voucher-mappings", async (ISender sender, CancellationToken ct) => await sender.Send(new ListInventoryVoucherMappingsQuery(), ct))
            .WithName("Accounting_Inventory_VoucherMappings_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapPut("/voucher-mappings", async (MapeoRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new SetInventoryVoucherMappingCommand(body.Operation ?? string.Empty, body.InventoryDocumentTypeCode,
                    body.VoucherTypePublicId, body.CrossDocumentTypePublicId, body.Reason ?? string.Empty), ct);
                return r.IsSuccess ? (object)Results.Ok(new { mappingPublicId = r.Value }) : r;
            })
            .WithName("Accounting_Inventory_VoucherMappings_Set").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasAdministrar);
    }

    private static void Lotes(RouteGroupBuilder g)
    {
        g.MapGet("/batches", async (DateOnly? from, DateOnly? to, BatchStatus? status, BatchTrigger? trigger, string? destination,
                    int? page, int? pageSize, ISender sender, IOptions<IntegrationOptions> opciones, CancellationToken ct) =>
                await sender.Send(new ListInventoryBatchesQuery(from, to, status, trigger, destination, new PageRequest(page ?? 1, pageSize ?? 20),
                    opciones.Value.Dispatcher.LateToleranceMinutes), ct))
            .WithName("Accounting_Inventory_Batches_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(LotesVer);

        g.MapPost("/batches/preview", async (VistaPreviaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewIntegrationBatchQuery(body.From, body.To, body.DocumentTypeCodes, body.ScheduleKey, body.BranchPublicId), ct))
            .WithName("Accounting_Inventory_Batches_Preview").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(LotesVer);

        g.MapPost("/batches", async (OrdenDeLoteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new OrderIntegrationBatchCommand(body.CutoffMessagePublicId, body.From, body.To, body.DocumentTypeCodes,
                    body.ScheduleKey, body.BranchPublicId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess
                    ? (object)Results.Accepted($"/api/accounting/inventory/batches/{r.Value.BatchPublicId}",
                        new { batchPublicId = r.Value.BatchPublicId, number = r.Value.Number, status = r.Value.Status })
                    : r;
            })
            .WithName("Accounting_Inventory_Batches_Order").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(LotesCorrer);

        g.MapGet("/batches/{id:guid}", async (Guid id, ISender sender, IOptions<IntegrationOptions> opciones, CancellationToken ct) =>
                await sender.Send(new GetInventoryBatchQuery(id, opciones.Value.Dispatcher.LateToleranceMinutes), ct))
            .WithName("Accounting_Inventory_Batches_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(LotesVer);

        g.MapGet("/postings", async (Guid? document, Guid? message, Guid? batch, DateOnly? from, DateOnly? to, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInventoryPostingsQuery(document, message, batch, from, to, new PageRequest(page ?? 1, pageSize ?? 50)), ct))
            .WithName("Accounting_Inventory_Postings_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ComprobantesVer);
    }

    private static IResult Sobre(HttpContext http, string codigo, string mensaje, int estado) =>
        Results.Json(new { code = codigo, message = mensaje, traceId = http.TraceIdentifier }, statusCode: estado);
}
