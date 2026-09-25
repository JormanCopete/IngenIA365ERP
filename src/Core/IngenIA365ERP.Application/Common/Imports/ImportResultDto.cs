using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Lo que responde toda importación, en revisión y en aplicación (feature 012, T49, T154; contracts/plantillas.md
/// §0.5, la única fuente de su forma). Con <c>mode=apply</c> y errores, el mismo cuerpo viaja en <c>data</c> del 422
/// <c>Import.Invalid</c>.
/// </summary>
public sealed record ImportResultDto
{
    /// <summary>La clave de la plantilla (<c>inventory.products</c>, <c>core.taxes</c>…; <c>CatalogoDePlantillas</c>).</summary>
    public string Template { get; init; } = string.Empty;
    public ModoDeImportacion Mode { get; init; }
    public bool Valid { get; init; }
    public bool Applied { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileSha256 { get; init; } = string.Empty;
    public bool RequiresReason { get; init; }
    public IReadOnlyList<ResumenDeHojaDto> Sheets { get; init; } = [];
    /// <summary>Hasta <see cref="EjecutorDeImportacion.MaximoDeCambios"/>; el resto, en la revisión en Excel.</summary>
    public IReadOnlyList<CambioDeFilaDto> Changes { get; init; } = [];
    public bool ChangesTruncated { get; init; }
    public IReadOnlyList<ErrorDeFila> Warnings { get; init; } = [];
    /// <summary>Hasta <see cref="EjecutorDeImportacion.MaximoDeErrores"/>, por hoja, fila y columna.</summary>
    public IReadOnlyList<ErrorDeFila> Errors { get; init; } = [];
    public int TotalErrors { get; init; }
    /// <summary>Lo propio de cada plantilla (§8, §14, §15 del contrato); vacío si no trae nada.</summary>
    public IReadOnlyDictionary<string, object?> Extra { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// El resultado de <b>cada</b> fila, sin tope, para devolver el mismo libro con las columnas <c>resultado</c> y
    /// <c>errores</c> (<c>format=xlsx</c>, §0.5). No viaja en el JSON: con 60.000 filas la respuesta sería el archivo.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ResultadoDeFila> Rows { get; init; } = [];
}

/// <summary>Conteos de una hoja (nuevo).</summary>
public sealed record ResumenDeHojaDto(string Sheet, int Rows, int Created, int Updated, int Unchanged);

/// <summary>Una fila que crea o actualiza (nuevo): la llave y los campos que cambian, con su valor antes y después.</summary>
public sealed record CambioDeFilaDto(string? Sheet, int Row, string Key, AccionDeImportacion Action, IReadOnlyList<CampoCambiadoDto> Fields);

/// <summary>Un campo que cambia (nuevo). <see cref="Before"/> es nulo al crear.</summary>
public sealed record CampoCambiadoDto(string Column, string? Before, string? After);

/// <summary>El resultado de una fila para la revisión en Excel (nuevo): su acción (nula si tuvo errores) y sus mensajes.</summary>
public sealed record ResultadoDeFila(string Sheet, int Row, AccionDeImportacion? Action, IReadOnlyList<string> Errors);
