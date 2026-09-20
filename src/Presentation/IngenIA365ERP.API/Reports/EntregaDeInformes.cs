using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Punto único de entrega de informes (nómina, contabilidad): JSON para la pantalla, archivo
/// para descargar. El error va en el sobre de siempre. Vivía como método privado de
/// <c>PayrollReportsEndpoints</c> (feature 006); la 009 lo comparte.
///
/// <para>
/// Hasta la E2 de la 009 (2026-09-20) todo fallo del handler salía <b>400</b>: un tercero o una
/// cuenta que no existen (<c>*.NotFound</c>) se confundían con un formato mal escrito, y un
/// informe sobre una contabilidad sin iniciar tampoco se distinguía. Ahora el status sigue la
/// misma regla que <see cref="ErrorEnvelopeFilter"/> (<c>Validation.*</c> 400, <c>*.NotFound</c>
/// 404, el resto 422) y el cuerpo conserva <c>errorCode</c> —lo leen las pantallas de nómina—
/// además de <c>code</c>, <c>message</c> y <c>data</c> cuando el error la trae.
/// </para>
/// </summary>
public static class EntregaDeInformes
{
    public const string Json = "json";
    public static readonly string[] FormatosDeArchivo = ["xlsx", "pdf", "docx"];

    /// <summary>
    /// Formato normalizado (<c>json</c> si viene nulo o vacío). El vacío cuenta como ausente porque
    /// así llega <c>format</c> cuando el filtro de exportación lo lee de la query string sin que
    /// nadie lo haya mandado: tratarlo como «otro formato» habría exigido el permiso de exportar
    /// a toda pantalla que consulta.
    /// </summary>
    public static string Normalizar(string? formato) => string.IsNullOrWhiteSpace(formato) ? Json : formato.Trim().ToLowerInvariant();

    /// <summary>Verdadero cuando el formato pide un archivo (y por tanto es una exportación auditable).</summary>
    public static bool EsExportacion(string? formato) => Normalizar(formato) != Json;

    public static bool EsFormatoValido(string? formato)
    {
        var f = Normalizar(formato);
        return f == Json || FormatosDeArchivo.Contains(f);
    }

    public static Task<IResult> EntregarAsync(Result<TablaExportable> resultado, string? formato, string nombreBase)
    {
        if (resultado.IsFailure)
            return Task.FromResult(Fallo(resultado.Error));
        var f = Normalizar(formato);
        if (f == Json) return Task.FromResult(Results.Ok(resultado.Value));
        if (!FormatosDeArchivo.Contains(f))
            return Task.FromResult(Fallo(new Error("Reportes.FormatoInvalido", "Formatos: json, xlsx, pdf, docx."), StatusCodes.Status400BadRequest));
        var archivo = ExportadorDeTablas.Exportar(resultado.Value, f, $"{nombreBase}-{DateTime.UtcNow:yyyyMMdd-HHmm}");
        return Task.FromResult(Results.File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo));
    }

    /// <summary>
    /// El sobre del fallo: <c>{ code, errorCode, message, data? }</c> con el status que dicta el código.
    /// Un formato desconocido es una petición mal formada y sigue siendo 400 (la prueba de
    /// integración de nómina lo afirma y su código no cambia): se le fija el status a mano.
    /// </summary>
    private static IResult Fallo(Error error, int? statusFijo = null)
    {
        var code = string.IsNullOrEmpty(error.Code) ? "Generic.Failure" : error.Code;
        var status = statusFijo ?? ErrorEnvelopeFilter.EstadoHttpDe(code);
        return error is ErrorConDatos conDatos
            ? Results.Json(new { code, errorCode = code, message = error.Message, data = conDatos.Data }, statusCode: status)
            : Results.Json(new { code, errorCode = code, message = error.Message }, statusCode: status);
    }
}
