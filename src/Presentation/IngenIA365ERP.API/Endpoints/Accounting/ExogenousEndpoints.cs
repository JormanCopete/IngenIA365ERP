using Carter;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Accounting.Exogenous;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// Información exógena (feature 009 E4, US11): formatos por año (semilla, copia del año anterior, edición), generación por formato con sus
/// inconsistencias, la tabla de revisión (json o Excel) y el XML del prevalidador. <c>Accounting.Exogenous.View</c> para leer,
/// <c>.Manage</c> para parametrizar y generar, <c>.Export</c> para el Excel y el XML.
/// </summary>
public class ExogenousEndpoints : ICarterModule
{
    /// <summary>Cuerpo de <c>PUT /formats/{year}/{code}</c>.</summary>
    public sealed record GuardarFormatoRequest(int FormatVersion, string Name, bool Applies, decimal MinAmount, bool MinorAmountsRule, string MinorAmountsTaxId,
        DisposicionDeExogena Layout, IReadOnlyList<ConceptoDeExogenaDto> Concepts);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/exogenous")
            .WithTags("AccountingExogenous")
            .RequireAuthorization();

        group.MapGet("/formats", async ([FromQuery] int year, ISender sender, CancellationToken ct) => await sender.Send(new ListExogenousFormatsQuery(year), ct))
            .WithName("Accounting_Exogenous_Formats")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.View");

        group.MapPost("/formats/{year:int}/load-seed", async (int year, ISender sender, CancellationToken ct) => await sender.Send(new LoadExogenousSeedCommand(year), ct))
            .WithName("Accounting_Exogenous_LoadSeed")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.Manage");

        group.MapPost("/formats/{year:int}/copy-from/{fromYear:int}", async (int year, int fromYear, ISender sender, CancellationToken ct) =>
                await sender.Send(new CopyExogenousYearCommand(fromYear, year), ct))
            .WithName("Accounting_Exogenous_CopyYear")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.Manage");

        group.MapPut("/formats/{year:int}/{code}", async (int year, string code, GuardarFormatoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveExogenousFormatCommand(year, code, body.FormatVersion, body.Name ?? string.Empty, body.Applies, body.MinAmount, body.MinorAmountsRule,
                    body.MinorAmountsTaxId ?? string.Empty, body.Layout, body.Concepts ?? []), ct))
            .WithName("Accounting_Exogenous_SaveFormat")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.Manage");

        // El plano de conceptos y cuentas del año (2026-10-31): se baja lleno, se edita y se sube; cada formato del archivo reemplaza sus conceptos.
        group.MapGet("/formats/{year:int}/plano.xlsx", async (int year, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var tabla = await sender.Send(new ExportExogenousConceptsQuery(year), ct);
                if (tabla.IsFailure) return ErrorEnvelopeFilter.Translate(http, tabla);
                var archivo = PlantillaDeImportacion.Xlsx(tabla.Value, $"exogena-{year}-conceptos");
                return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
            })
            .WithName("Accounting_Exogenous_ConceptsFile")
            .RequirePermission("Accounting.Exogenous.View");

        group.MapPost("/formats/{year:int}/import", async (int year, [FromForm] IFormFile archivo, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var (contenido, rechazo) = await LeerArchivoAsync(archivo, http, ct);
                return rechazo ?? await sender.Send(new ImportExogenousConceptsCommand(year, archivo.FileName, contenido!), ct);
            })
            .WithName("Accounting_Exogenous_ImportConcepts")
            .DisableAntiforgery()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.Manage");

        group.MapGet("/runs", async ([FromQuery] int year, ISender sender, CancellationToken ct) => await sender.Send(new ListExogenousRunsQuery(year), ct))
            .WithName("Accounting_Exogenous_Runs")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.View");

        group.MapPost("/runs/{year:int}/{code}", async (int year, string code, ISender sender, CancellationToken ct) => await sender.Send(new GenerateExogenousRunCommand(year, code), ct))
            .WithName("Accounting_Exogenous_Generate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.Manage");

        group.MapGet("/runs/{id:guid}/issues", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new ListExogenousIssuesQuery(id), ct))
            .WithName("Accounting_Exogenous_Issues")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.View");

        // La tabla de revisión: json para la pantalla; xlsx/pdf/docx exigen además el permiso de exportar.
        group.MapGet("/runs/{id:guid}/table", async (Guid id, [FromQuery] string? format, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new ExogenousRunTableQuery(id, format), ct), format, "exogena"))
            .WithName("Accounting_Exogenous_Table")
            .RequirePermission("Accounting.Exogenous.View")
            .RequirePermissionWhenExporting("Accounting.Exogenous.Export");

        group.MapPost("/runs/{id:guid}/xml", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ExportExogenousXmlCommand(id), ct);
                return result.IsSuccess ? Results.File(result.Value.Content, "application/xml", result.Value.FileName) : (object)result;
            })
            .WithName("Accounting_Exogenous_Xml")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Exogenous.Export");
    }

    /// <summary>El archivo de un plano subido por formulario (mismo molde que la carga de auxiliares): vacío es 400, más de 10 MB es 413.</summary>
    private static async Task<(byte[]? Contenido, object? Rechazo)> LeerArchivoAsync(IFormFile? archivo, HttpContext http, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return (null, Results.Json(new { code = "Archivo.Vacio", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest));
        if (archivo.Length > OpeningEndpoints.MaxBytes)
            return (null, Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 10 MB.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status413PayloadTooLarge));
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        return (ms.ToArray(), null);
    }
}
