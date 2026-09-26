using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Los códigos comunes de toda importación (feature 012, T49, T154; contracts/plantillas.md §0.3–§0.5). No hay códigos
/// de importación propios de cada área: todas responden <c>Import.*</c>, y las reglas de negocio con el mismo código
/// que el alta unitaria. (nuevo)
/// </summary>
public static class ImportErrors
{
    public const string InvalidCode = "Import.Invalid";
    public const string ModeRequiredCode = "Import.ModeRequired";
    public const string CellFormat = "Import.Cell.Format";
    public const string CellRequired = "Import.Cell.Required";
    public const string CellNotFound = "Import.Cell.NotFound";
    public const string CellNotYetAvailable = "Import.Cell.NotYetAvailable";
    public const string CellPermissionRequired = "Import.Cell.PermissionRequired";
    public const string RowDuplicate = "Import.Row.Duplicate";
    public const string ColumnUnknown = "Import.Column.Unknown";
    public const string HojaFaltanteCode = ArchivosTabulares.HojaFaltanteCode;

    /// <summary>La columna del motivo cuando la revisión lo pide (§0.5).</summary>
    public const string ColumnaDelMotivo = "reason";

    /// <summary>400: la ruta llegó sin <c>mode=review|apply</c>.</summary>
    public static readonly Error ModeRequired = new(ModeRequiredCode,
        "Diga si revisa o aplica el archivo: mode=review o mode=apply.");

    /// <summary>422: <c>mode=apply</c> con errores; el cuerpo completo va en <c>data</c> y no se guardó nada.</summary>
    public static Error Invalid(ImportResultDto resultado)
    {
        var primero = resultado.Errors.FirstOrDefault();
        var donde = primero is null ? string.Empty
            : primero.Row > 0
                ? $" (el primero, {(primero.Sheet is null ? string.Empty : $"hoja {primero.Sheet}, ")}fila {primero.Row}: {primero.Message})"
                : $" (el primero: {primero.Message})";
        return new ErrorConDatos(InvalidCode,
            $"El archivo tiene {resultado.TotalErrors} error(es){donde}; no se guardó nada.",
            resultado);
    }

    /// <summary>400: falta una hoja obligatoria del libro.</summary>
    public static Error HojaFaltante(string hoja) => ArchivosTabulares.HojaFaltante(hoja);
}
