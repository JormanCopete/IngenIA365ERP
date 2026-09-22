using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Accounting.Opening;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// Saldos de apertura (feature 009 E2, US13; contracts/api.md §7). La plantilla es un <c>.xlsx</c>
/// con los encabezados en la fila 1 y las instrucciones en otra hoja; importar deja un borrador
/// <c>AP</c> que se contabiliza y se reversa por <c>/api/accounting/documents</c>, como cualquier
/// comprobante. Todo bajo <c>Accounting.Opening.Manage</c>; sin permiso, 404 como el resto.
/// </summary>
public class OpeningEndpoints : ICarterModule
{
    /// <summary>Un archivo de apertura de 5 000 filas cabe holgado; más que eso es un archivo mal armado.</summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/opening")
            .WithTags("AccountingOpening")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new GetOpeningStatusQuery(), ct))
            .WithName("Accounting_Opening_Status")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Opening.Manage");

        group.MapGet("/template.xlsx", async (ISender sender, HttpContext http, CancellationToken ct) =>
            {
                var tabla = await sender.Send(new GetOpeningTemplateQuery(), ct);
                if (tabla.IsFailure) return ErrorEnvelopeFilter.Translate(http, tabla);
                var archivo = PlantillaDeImportacion.Xlsx(tabla.Value, "plantilla-saldos-de-apertura");
                return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
            })
            .WithName("Accounting_Opening_Template")
            .RequirePermission("Accounting.Opening.Manage");

        group.MapPost("/import", ImportarAsync)
            .WithName("Accounting_Opening_Import")
            .DisableAntiforgery()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Opening.Manage");
    }

    private static async Task<object?> ImportarAsync([FromForm] IFormFile archivo, ISender sender, HttpContext http, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return Results.Json(new { code = "Archivo.Vacio", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);
        if (archivo.Length > MaxBytes)
            return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 10 MB.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status413PayloadTooLarge);

        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        var result = await sender.Send(new ImportOpeningBalancesCommand(archivo.FileName, ms.ToArray()), ct);
        return result.IsSuccess ? Results.Created($"/api/accounting/documents/{result.Value.DraftPublicId}", result.Value) : result;
    }
}
