using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Integration;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// El lado contable de la integración con Cartera (feature 013, US7, T0661; contracts/contabilidad.md §21.1, api.md §23; enmienda E-01 de
/// la 009). Molde: <see cref="InventoryIntegrationEndpoints"/>. Grupo <c>/api/accounting/lending</c>: la matriz de reglas y su plantilla,
/// «¿a qué cuenta va?», la completitud, el mapeo de tipos de comprobante, los lotes de hechos de Cartera con su vista previa y su orden
/// manual, y los recibos; grupo <c>/api/accounting/regulatory-indicators</c>: el patrimonio técnico y la relación de solvencia con su
/// soporte. Todo con <c>RequireAuthorization</c>, el sobre de errores y un permiso por ruta; sin permiso, el mismo 404 que lo inexistente.
///
/// <para>
/// La matriz, el mapeo y los indicadores siguen la mecánica de la 009, sin clave de operación. Llevan <c>Idempotency-Key</c> la orden de
/// lote (202, con motivo, <c>OriginModule = CAR</c>) y la importación de la plantilla. Los consumidores (<c>PostLendingFactsCommand</c>,
/// <c>PostLendingSummaryGroupCommand</c>) y los cinco métodos de <c>IContabilidadParaCartera</c> no tienen ruta
/// (<c>LosComandosDeConsumoNoTienenRuta</c>). (nuevo)
/// </para>
/// </summary>
public class LendingIntegrationEndpoints : ICarterModule
{
    public const string ReglasVer = "Accounting.LendingRules.View";
    public const string ReglasAdministrar = "Accounting.LendingRules.Manage";
    public const string LotesVer = "Accounting.LendingBatches.View";
    public const string LotesCorrer = "Accounting.LendingBatches.Run";
    public const string ComprobantesVer = "Accounting.Vouchers.View";
    public const string IndicadoresVer = "Accounting.RegulatoryIndicators.View";
    public const string IndicadoresAdministrar = "Accounting.RegulatoryIndicators.Manage";

    private const long TopeDelCuerpo = EjecutorDeImportacion.MaximoDeBytes + 256 * 1024;

    public sealed record CrearReglaRequest(
        string? Operation,
        string? Role,
        DimensionesDeReglaDeCarteraDto? Dimensions,
        Guid AccountPublicId,
        DateOnly ValidFrom,
        string? Notes,
        string? Reason,
        DateOnly? ValidTo);

    public sealed record VersionDeReglaRequest(Guid AccountPublicId, DateOnly ValidFrom, string? Notes, string? Reason, Guid? CostCenterPublicId);

    public sealed record DesactivarReglaRequest(DateOnly ValidTo, string? Reason);

    public sealed record ResolverRequest(string? Operation, string? Role, DimensionesDeReglaDeCarteraDto? Dimensions, DateOnly? Date);

    public sealed record MapeoRequest(
        string? Operation,
        string? CollectorClass,
        Guid VoucherTypePublicId,
        Guid? CrossDocumentTypePublicId,
        string? Reason);

    /// <summary>
    /// El alcance de la vista previa y del lote manual: rango de fechas contables, horario (<c>CAR.…</c>) y sucursal. Los tipos de hecho no
    /// filtran: el lote toma, en orden de emisión, todo lo de Cartera del alcance hasta el corte, para no dejar dependientes atrás.
    /// </summary>
    public sealed record VistaPreviaRequest(DateOnly From, DateOnly To, string? ScheduleKey, Guid? BranchPublicId);

    public sealed record OrdenDeLoteRequest(
        Guid CutoffMessagePublicId,
        DateOnly From,
        DateOnly To,
        string? ScheduleKey,
        Guid? BranchPublicId,
        string? Reason);

    public sealed record RegistrarIndicadorRequest(
        RegulatoryIndicatorKind Kind,
        DateOnly CutoffDate,
        decimal Value,
        string? Source,
        Guid? SupportAttachmentPublicId,
        Guid? SupersedesPublicId,
        string? Notes,
        Guid? PublicId);

    /// <summary>El detalle de un lote con sus totales (débito y crédito de los comprobantes que dejaron sus recibos).</summary>
    public sealed record DetalleDeLoteConTotales(LendingBatchDetailDto Detail, TotalesDeLoteEnDestino Totals);

    /// <summary>
    /// Un indicador con su vigencia (api.md §23.6): <c>Current</c> si no fue reemplazado, es el último de su tipo y su corte no es más viejo
    /// que <c>Contabilidad.IndicadoresVigenciaMeses</c>; <c>ExpiresOn</c> = corte + esos meses.
    /// </summary>
    public sealed record IndicadorConVigencia(RegulatoryIndicatorDto Indicator, bool Current, DateOnly? ExpiresOn);

    /// <summary>La lista de indicadores y los meses de vigencia con que se juzgó (los mismos que usa Cartera, C33).</summary>
    public sealed record IndicadoresConVigencia(IReadOnlyList<IndicadorConVigencia> Items, int? ValidityMonths);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/accounting/lending")
            .WithTags("Accounting Lending Integration")
            .RequireAuthorization();

        Reglas(g);
        Mapeos(g);
        Lotes(g);

        var indicadores = app.MapGroup("/api/accounting/regulatory-indicators")
            .WithTags("Accounting Regulatory Indicators")
            .RequireAuthorization();
        Indicadores(indicadores);
    }

    private static void Reglas(RouteGroupBuilder g)
    {
        g.MapGet("/rules/catalog", async (ISender sender, CancellationToken ct) => await sender.Send(new GetLendingRulesCatalogQuery(), ct))
            .WithName("Accounting_Lending_Rules_Catalog").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapGet("/rules", async (string? operation, string? role, DateOnly? asOf, string? creditLineCode, string? modalityCode, string? subsegmentCode,
                    string? riskCategory, string? conceptCode, string? collectorClass, string? account, bool? onlyCurrent, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLendingPostingRulesQuery(operation, role, asOf, creditLineCode, modalityCode, subsegmentCode, riskCategory,
                    conceptCode, collectorClass, account, onlyCurrent ?? false, new PageRequest(page ?? 1, pageSize ?? 50)), ct))
            .WithName("Accounting_Lending_Rules_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapGet("/rules/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLendingPostingRuleQuery(id), ct))
            .WithName("Accounting_Lending_Rules_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapGet("/rules/{id:guid}/versions", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLendingPostingRuleVersionsQuery(id), ct))
            .WithName("Accounting_Lending_Rules_Versions").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapPost("/rules", async (CrearReglaRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateLendingPostingRuleCommand(body.Operation, body.Role ?? string.Empty,
                    body.Dimensions ?? new DimensionesDeReglaDeCarteraDto(), body.AccountPublicId, body.ValidFrom, body.Notes, body.Reason ?? string.Empty,
                    body.ValidTo), ct);
                return r.IsSuccess ? (object)Results.Created($"/api/accounting/lending/rules/{r.Value.RulePublicId}", r.Value) : r;
            })
            .WithName("Accounting_Lending_Rules_Create").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasAdministrar);

        g.MapPost("/rules/{id:guid}/versions", async (Guid id, VersionDeReglaRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new AddLendingPostingRuleVersionCommand(id, body.AccountPublicId, body.ValidFrom, body.Notes,
                    body.Reason ?? string.Empty, body.CostCenterPublicId), ct);
                return r.IsSuccess ? (object)Results.Created($"/api/accounting/lending/rules/{r.Value.RulePublicId}", r.Value) : r;
            })
            .WithName("Accounting_Lending_Rules_AddVersion").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasAdministrar);

        g.MapPost("/rules/{id:guid}/deactivate", async (Guid id, DesactivarReglaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeactivateLendingPostingRuleCommand(id, body.ValidTo, body.Reason ?? string.Empty), ct))
            .WithName("Accounting_Lending_Rules_Deactivate").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasAdministrar);

        // «¿A qué cuenta va esta combinación?» (§3.7): sin clave de operación; no es un error que no haya regla, la respuesta lo dice.
        g.MapPost("/rules/resolve", async (ResolverRequest body, ISender sender, IDateTimeService reloj, CancellationToken ct) =>
                await sender.Send(new ResolveLendingRuleQuery(body.Operation ?? string.Empty, body.Role ?? string.Empty,
                    body.Dimensions ?? new DimensionesDeReglaDeCarteraDto(), body.Date ?? reloj.HoyLocal), ct))
            .WithName("Accounting_Lending_Rules_Resolve").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        // La plantilla de la matriz (§3.7): encabezados en la fila 1 e instrucciones en otra hoja. Con datos, las reglas vigentes y futuras y
        // las filas sugeridas sin cuenta, «pendiente de validar» (C70).
        g.MapGet("/rules/template.xlsx", async ([FromQuery] bool? withData, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                const string nombre = "plantilla-matriz-contable-de-cartera";
                if (withData != true)
                {
                    var vacia = PlantillaDeImportacion.Xlsx(PlantillaDeMatrizDeCartera.Definicion, nombre);
                    return Results.File(vacia.Contenido, vacia.TipoContenido, vacia.NombreArchivo);
                }

                var datos = await sender.Send(new GetLendingRulesTemplateQuery(), ct);
                if (datos.IsFailure) return ErrorEnvelopeFilter.Translate(http, datos);
                var llena = PlantillaDeImportacion.Xlsx(PlantillaDeMatrizDeCartera.Definicion, nombre + "-datos", datos.Value);
                return Results.File(llena.Contenido, llena.TipoContenido, llena.NombreArchivo);
            })
            .WithName("Accounting_Lending_Rules_Template").RequirePermission(ReglasVer);

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
                var resultado = await sender.Send(new ImportLendingPostingRulesCommand(modo, subido, motivo) { OperationKey = http.ClaveDeOperacion() }, ct);

                if (resultado.IsSuccess && resultado.Value.Mode == ModoDeImportacion.Review
                    && string.Equals(http.Request.Query["format"].ToString(), "xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    var revisado = PlantillaDeImportacion.ConResultados(subido, PlantillaDeMatrizDeCartera.Definicion, resultado.Value);
                    return Results.File(revisado.Contenido, revisado.TipoContenido, revisado.NombreArchivo);
                }
                return (object)resultado;
            })
            .WithName("Accounting_Lending_Rules_Import")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(TopeDelCuerpo))
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(ReglasAdministrar);

        g.MapGet("/completeness", async (DateOnly? date, ISender sender, IDateTimeService reloj, CancellationToken ct) =>
                await sender.Send(new GetLendingRulesCompletenessQuery(date ?? reloj.HoyLocal), ct))
            .WithName("Accounting_Lending_Completeness").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);
    }

    private static void Mapeos(RouteGroupBuilder g)
    {
        g.MapGet("/voucher-mappings", async (ISender sender, CancellationToken ct) => await sender.Send(new ListLendingVoucherMappingsQuery(), ct))
            .WithName("Accounting_Lending_VoucherMappings_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasVer);

        g.MapPut("/voucher-mappings", async (MapeoRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new SetLendingVoucherMappingCommand(body.Operation ?? string.Empty, body.CollectorClass,
                    body.VoucherTypePublicId, body.CrossDocumentTypePublicId, body.Reason ?? string.Empty), ct);
                return r.IsSuccess ? (object)Results.Ok(new { mappingPublicId = r.Value }) : r;
            })
            .WithName("Accounting_Lending_VoucherMappings_Set").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ReglasAdministrar);
    }

    private static void Lotes(RouteGroupBuilder g)
    {
        g.MapGet("/batches", async (DateOnly? from, DateOnly? to, BatchStatus? status, BatchTrigger? trigger, int? page, int? pageSize,
                    ISender sender, IOptions<IntegrationOptions> opciones, CancellationToken ct) =>
                await sender.Send(new ListLendingBatchesQuery(from, to, status, trigger, new PageRequest(page ?? 1, pageSize ?? 20),
                    opciones.Value.Dispatcher.LateToleranceMinutes), ct))
            .WithName("Accounting_Lending_Batches_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(LotesVer);

        // La vista previa del lote manual (§10.5): las entregas InBatch sin lote de hechos CAR del alcance; no numera ni guarda y trae el
        // corte que se manda al ordenarlo.
        g.MapPost("/batches/preview", async (VistaPreviaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewLendingBatchScopeQuery(body.From, body.To, body.ScheduleKey, body.BranchPublicId), ct))
            .WithName("Accounting_Lending_Batches_Preview").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(LotesVer);

        g.MapPost("/batches", async (OrdenDeLoteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new OrderIntegrationBatchCommand(body.CutoffMessagePublicId, body.From, body.To, null,
                    body.ScheduleKey, body.BranchPublicId, body.Reason ?? string.Empty)
                    { OperationKey = http.ClaveDeOperacion(), OriginModule = ModulosDeOrigen.Cartera }, ct);
                return r.IsSuccess
                    ? (object)Results.Accepted($"/api/accounting/lending/batches/{r.Value.BatchPublicId}",
                        new { batchPublicId = r.Value.BatchPublicId, number = r.Value.Number, status = r.Value.Status })
                    : r;
            })
            .WithName("Accounting_Lending_Batches_Order").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(LotesCorrer);

        g.MapGet("/batches/{id:guid}", async (Guid id, ISender sender, IOptions<IntegrationOptions> opciones, CancellationToken ct) =>
            {
                var detalle = await sender.Send(new GetLendingBatchQuery(id, opciones.Value.Dispatcher.LateToleranceMinutes), ct);
                if (detalle.IsFailure) return (object)detalle;
                var totales = await sender.Send(new GetLendingBatchTotalsQuery(id), ct);
                return Results.Ok(new DetalleDeLoteConTotales(detalle.Value, totales.IsSuccess ? totales.Value : TotalesDeLoteEnDestino.Cero));
            })
            .WithName("Accounting_Lending_Batches_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(LotesVer);

        g.MapGet("/postings", async (Guid? document, Guid? source, Guid? message, Guid? batch, DateOnly? from, DateOnly? to, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLendingPostingsQuery(document, source, message, batch, from, to, new PageRequest(page ?? 1, pageSize ?? 50)), ct))
            .WithName("Accounting_Lending_Postings_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(ComprobantesVer);
    }

    private static void Indicadores(RouteGroupBuilder g)
    {
        g.MapGet("/", async (RegulatoryIndicatorKind? code, DateOnly? asOf, ISender sender, ILectorDeParametros parametros, IDateTimeService reloj,
                CancellationToken ct) =>
            {
                var lista = await sender.Send(new ListRegulatoryIndicatorsQuery(code), ct);
                if (lista.IsFailure) return (object)lista;

                var fecha = asOf ?? reloj.HoyLocal;
                var meses = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.ContabilidadIndicadoresVigenciaMeses, fecha, ct: ct);
                var vigencia = meses.IsSuccess && meses.Value.Valor is int m ? m : (int?)null;
                var ultimos = lista.Value
                    .Where(i => i.SupersededByPublicId is null && i.CutoffDate <= fecha)
                    .GroupBy(i => i.Kind)
                    .Select(gr => gr.OrderByDescending(i => i.CutoffDate).ThenByDescending(i => i.RegisteredAt).First().RegulatoryIndicatorPublicId)
                    .ToHashSet();
                var items = lista.Value
                    .Where(i => asOf is null || i.CutoffDate <= fecha)
                    .Select(i =>
                    {
                        DateOnly? vence = vigencia is { } v ? i.CutoffDate.AddMonths(v) : null;
                        var vigente = ultimos.Contains(i.RegulatoryIndicatorPublicId) && (vence is not { } hasta || hasta >= fecha);
                        return new IndicadorConVigencia(i, vigente, vence);
                    })
                    .ToList();
                return Results.Ok(new IndicadoresConVigencia(items, vigencia));
            })
            .WithName("Accounting_RegulatoryIndicators_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(IndicadoresVer);

        g.MapPost("/", async (RegistrarIndicadorRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RegisterRegulatoryIndicatorCommand(body.Kind, body.CutoffDate, body.Value, body.Source ?? string.Empty,
                    body.SupportAttachmentPublicId, body.SupersedesPublicId, body.Notes, body.PublicId), ct);
                return r.IsSuccess
                    ? (object)Results.Created($"/api/accounting/regulatory-indicators/{r.Value}", new { regulatoryIndicatorPublicId = r.Value })
                    : r;
            })
            .WithName("Accounting_RegulatoryIndicators_Register").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(IndicadoresAdministrar);
    }

    private static IResult Sobre(HttpContext http, string codigo, string mensaje, int estado) =>
        Results.Json(new { code = codigo, message = mensaje, traceId = http.TraceIdentifier }, statusCode: estado);
}
