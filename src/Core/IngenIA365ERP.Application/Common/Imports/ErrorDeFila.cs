using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Una fila con error (o con aviso) en una importación; nada se guarda a medias (FR-005 de la 009, FR-030 de la 012).
///
/// <para>
/// Nació en la feature 009 como <c>Application.Accounting.Setup.ErrorDeFila</c> (cuentas, apertura, catálogos). Desde
/// la feature 012 (T49, T154) vive aquí, porque todas las plantillas la comparten, y ganó <see cref="Sheet"/>: la hoja
/// del libro, nula en las plantillas de una sola hoja. Nula no viaja en el JSON, así que lo que responde la 009 no cambia.
/// </para>
/// </summary>
/// <param name="Row">La fila de Excel (el encabezado es la 1). <c>0</c> = el error no es de una fila (el motivo, el archivo).</param>
/// <param name="Column">El encabezado de la columna, como lo escribe la plantilla; vacío si no es de una columna.</param>
/// <param name="Code">El código de error (<c>Import.Cell.Format</c>, o el mismo del alta unitaria).</param>
/// <param name="Message">El mensaje para la persona.</param>
/// <param name="Sheet">La hoja; nula en las plantillas de una sola hoja.</param>
public sealed record ErrorDeFila(
    int Row,
    string Column,
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Sheet = null);
