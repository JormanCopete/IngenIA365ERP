using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Punto único de entrega de informes (nómina, contabilidad): JSON para la pantalla, archivo
/// para descargar. El error va en el sobre de siempre. Vivía como método privado de
/// <c>PayrollReportsEndpoints</c> (feature 006); la 009 lo comparte.
/// </summary>
public static class EntregaDeInformes
{
    public const string Json = "json";
    public static readonly string[] FormatosDeArchivo = ["xlsx", "pdf", "docx"];

    /// <summary>Formato normalizado (<c>json</c> si viene vacío).</summary>
    public static string Normalizar(string? formato) => (formato ?? Json).Trim().ToLowerInvariant();

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
            return Task.FromResult(Results.BadRequest(new { code = resultado.Error.Code, errorCode = resultado.Error.Code, message = resultado.Error.Message }));
        var f = Normalizar(formato);
        if (f == Json) return Task.FromResult(Results.Ok(resultado.Value));
        if (!FormatosDeArchivo.Contains(f))
            return Task.FromResult(Results.BadRequest(new { code = "Reportes.FormatoInvalido", errorCode = "Reportes.FormatoInvalido", message = "Formatos: json, xlsx, pdf, docx." }));
        var archivo = ExportadorDeTablas.Exportar(resultado.Value, f, $"{nombreBase}-{DateTime.UtcNow:yyyyMMdd-HHmm}");
        return Task.FromResult(Results.File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo));
    }
}
