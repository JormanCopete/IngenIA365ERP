using System.Globalization;
using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Implantacion.Cotejo;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Implantacion;

/// <summary>
/// El cotejo con cifras de referencia de la implantación (2026-11-28; nuevo). Por cada opción —<c>cuentas</c>, <c>terceros</c>,
/// <c>creditos</c>, <c>cuentas-de-asociados</c>, <c>asociados</c>— dos rutas con el permiso de la opción (sin él, 404 como el resto):
/// <list type="bullet">
/// <item><c>GET /api/implantacion/cotejo/{opcion}/template.xlsx</c>: la plantilla (encabezados en la fila 1, instrucciones en otra hoja);</item>
/// <item><c>POST /api/implantacion/cotejo/{opcion}?asOf=&amp;level=resumen|detalle&amp;group=&amp;onlyDifferences=&amp;debitMinusCredit=&amp;tolerance=&amp;format=</c>
/// con el archivo en <c>multipart/form-data</c> (campo <c>archivo</c>, hasta 10 MB): el resumen o el detalle en json, o en archivo con
/// <c>format=xlsx|pdf|docx</c>.</item>
/// </list>
/// No guarda nada: cada cotejo vuelve a leer el archivo y a calcular, y deja su evento de auditoría.
/// </summary>
public class CotejoEndpoints : ICarterModule
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/implantacion/cotejo")
            .WithTags("ImplantacionCotejo")
            .RequireAuthorization();

        foreach (var (opcion, permiso) in OpcionesDeCotejo.Permisos)
        {
            var nombre = string.Concat(opcion.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
            group.MapGet($"/{opcion}/template.xlsx", async (ISender sender, HttpContext http, CancellationToken ct) =>
                {
                    var tabla = await sender.Send(new GetCotejoTemplateQuery(opcion), ct);
                    if (tabla.IsFailure) return ErrorEnvelopeFilter.Translate(http, tabla);
                    var archivo = PlantillaDeImportacion.Xlsx(tabla.Value, $"plantilla-cifras-de-referencia-{opcion}");
                    return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
                })
                .WithName($"Implantacion_Cotejo_{nombre}_Template")
                .RequirePermission(permiso);

            group.MapPost($"/{opcion}", (IFormFile archivo, [FromQuery] string? asOf, [FromQuery] string? level, [FromQuery(Name = "group")] string? grupo,
                    [FromQuery] bool? onlyDifferences, [FromQuery] bool? debitMinusCredit, [FromQuery] decimal? tolerance, [FromQuery] string? format,
                    ISender sender, HttpContext http, CancellationToken ct) =>
                    CotejarAsync(opcion, archivo, asOf, level, grupo, onlyDifferences, debitMinusCredit, tolerance, format, sender, http, ct))
                .WithName($"Implantacion_Cotejo_{nombre}")
                .RequireRateLimiting(LimiteDeCargas.Politica)
                .DisableAntiforgery()
                .RequirePermission(permiso);
        }
    }

    private static async Task<IResult> CotejarAsync(string opcion, IFormFile? archivo, string? asOf, string? level, string? grupo, bool? onlyDifferences,
        bool? debitMinusCredit, decimal? tolerance, string? format, ISender sender, HttpContext http, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return Results.Json(new { code = "Archivo.Vacio", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);
        if (archivo.Length > MaxBytes)
            return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 10 MB.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status413PayloadTooLarge);
        if (!EntregaDeInformes.EsFormatoValido(format))
            return Results.Json(new { code = "Reportes.FormatoInvalido", message = "Formatos: json, xlsx, pdf, docx.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);

        DateOnly? corte = null;
        if (!string.IsNullOrWhiteSpace(asOf))
        {
            if (!DateOnly.TryParseExact(asOf, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return Results.Json(new { code = "Validation.Date", message = "El corte va en formato yyyy-MM-dd.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);
            corte = d;
        }

        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        var resultado = await sender.Send(new CotejarCifrasQuery(opcion, archivo.FileName, ms.ToArray(), corte, level, onlyDifferences ?? false, grupo,
            debitMinusCredit ?? false, tolerance ?? 0m, format), ct);
        return await EntregaDeInformes.EntregarAsync(resultado, format, $"cotejo-{opcion}", http);
    }
}
