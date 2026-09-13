using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Payroll.Reports;

/// <summary>
/// Tipo de dato de una columna: decide alineación y formato en pantalla y en los archivos.
/// Viaja en JSON <b>por nombre</b> («Moneda», no 2): la pantalla lo lee como texto
/// (<c>ColumnaReporteDto.Tipo</c>) y hasta el 2026-09-13 la API lo mandaba como número, así que
/// Reportes de nómina fallaba al deserializar con «DeserializeUnableToConvertValue … $.columnas[0].tipo».
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoDeColumna
{
    Texto = 0,
    Entero = 1,
    Moneda = 2,
    Decimal = 3,
    Fecha = 4,
}

public sealed record ColumnaExportable(string Nombre, TipoDeColumna Tipo = TipoDeColumna.Texto, string? Clave = null);

/// <summary>
/// Un reporte como tabla: lo que la pantalla pinta y lo que los exportadores (Excel, PDF,
/// Word) convierten sin saber de nómina. Los valores van como texto ya formateado (para
/// mostrar) y como crudo (para las celdas numéricas del archivo), así el archivo suma lo
/// mismo que se ve. Las secciones sirven para agrupar filas (devengos / deducciones… en el
/// comprobante) sin inventar otra estructura.
/// </summary>
public sealed record TablaExportable(
    string Titulo,
    string Subtitulo,
    IReadOnlyList<ColumnaExportable> Columnas,
    IReadOnlyList<FilaExportable> Filas,
    FilaExportable? Totales,
    IReadOnlyList<string> Notas)
{
    public static TablaExportable Vacia(string titulo, string subtitulo, IReadOnlyList<ColumnaExportable> columnas) =>
        new(titulo, subtitulo, columnas, [], null, []);
}

/// <summary>Una fila: un valor por columna. <see cref="Seccion"/> agrupa (puede ser nula).</summary>
public sealed record FilaExportable(IReadOnlyList<object?> Valores, string? Seccion = null, bool Resaltada = false);
