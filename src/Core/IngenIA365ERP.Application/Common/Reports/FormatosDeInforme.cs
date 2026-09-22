namespace IngenIA365ERP.Application.Common.Reports;

/// <summary>
/// La única regla de qué formato pide un informe y si eso es una exportación. La usan los dos
/// lados que deciden sobre el mismo dato: la API (<c>EntregaDeInformes</c>, que entrega el
/// archivo, y <c>PermisoDeExportacionFilter</c>, que exige el permiso de exportar) y los
/// handlers de Application (que auditan <c>Accounting.Report.Exported</c>).
///
/// <para>
/// Hasta el 2026-09-20 cada lado normalizaba por su cuenta y no igual: la API hacía
/// <c>Trim().ToLowerInvariant()</c> y <c>FiltrosDeInforme.EsExportacion</c> comparaba sin
/// <c>Trim()</c>. Con <c>format=%20json</c> el filtro no exigía el permiso, la API respondía la
/// tabla JSON de pantalla y el handler igual auditaba una exportación: quedaba en Mongo un
/// evento de alguien que no puede exportar y no exportó nada, y la auditoría de exportaciones
/// (contracts/api.md §8) dejaba de ser confiable. Por eso la regla vive aquí una sola vez.
/// </para>
/// </summary>
public static class FormatosDeInforme
{
    public const string Json = "json";

    /// <summary>Los formatos que se entregan como archivo (y por tanto son exportaciones auditables).</summary>
    public static readonly string[] DeArchivo = ["xlsx", "pdf", "docx"];

    /// <summary>
    /// Formato normalizado: <see cref="Json"/> si viene nulo, vacío o en blanco; si no, sin
    /// espacios y en minúsculas. El vacío cuenta como ausente porque así llega <c>format</c> cuando
    /// se lee de la query string sin que nadie lo haya mandado: tratarlo como «otro formato»
    /// habría exigido el permiso de exportar a toda pantalla que consulta.
    /// </summary>
    public static string Normalizar(string? formato) =>
        string.IsNullOrWhiteSpace(formato) ? Json : formato.Trim().ToLowerInvariant();

    /// <summary>Verdadero cuando el formato normalizado pide algo distinto de JSON.</summary>
    public static bool EsExportacion(string? formato) => Normalizar(formato) != Json;

    /// <summary>JSON o uno de los tres archivos, con cualquier mayúscula o espacio alrededor.</summary>
    public static bool EsValido(string? formato)
    {
        var f = Normalizar(formato);
        return f == Json || DeArchivo.Contains(f);
    }
}
