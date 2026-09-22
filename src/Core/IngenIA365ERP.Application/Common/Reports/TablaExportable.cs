using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Common.Reports;

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
/// Word) convierten sin saber del módulo que la produjo. Los valores van crudos (para las
/// celdas numéricas del archivo), así el archivo suma lo mismo que se ve. Las secciones
/// sirven para agrupar filas (devengos / deducciones; clase / grupo / cuenta) sin inventar
/// otra estructura.
///
/// <para>
/// Nació en nómina (feature 006) como <c>Application.Payroll.Reports</c>; desde la feature 009
/// vive aquí porque contabilidad produce las mismas tablas y las entrega por el mismo camino
/// (<c>EntregaDeInformes</c> en la API). Los nombres no cambiaron.
/// </para>
/// </summary>
public sealed record TablaExportable(
    string Titulo,
    string Subtitulo,
    IReadOnlyList<ColumnaExportable> Columnas,
    IReadOnlyList<FilaExportable> Filas,
    FilaExportable? Totales,
    IReadOnlyList<string> Notas)
{
    /// <summary>
    /// Feature 010 (FR-012, contracts/archivos.md §3.1): en Excel, además de la hoja completa, cada
    /// <see cref="FilaExportable.Seccion"/> va en su propia hoja con sus filas y su subtotal, para
    /// entregar a cada fondo de cesantías sólo lo suyo. PDF y Word no cambian. Apagado por defecto.
    /// </summary>
    public bool HojaPorSeccion { get; init; }

    public static TablaExportable Vacia(string titulo, string subtitulo, IReadOnlyList<ColumnaExportable> columnas) =>
        new(titulo, subtitulo, columnas, [], null, []);
}

/// <summary>Una fila: un valor por columna. <see cref="Seccion"/> agrupa (puede ser nula).</summary>
public sealed record FilaExportable(IReadOnlyList<object?> Valores, string? Seccion = null, bool Resaltada = false);
