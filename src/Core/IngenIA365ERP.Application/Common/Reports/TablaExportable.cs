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
    /// <summary>Un porcentaje ya expresado en puntos (12,5 = 12,5 %); dos decimales y el signo «%» al pintar (feature 009 E2, FR-063).</summary>
    Porcentaje = 5,
}

public sealed record ColumnaExportable(string Nombre, TipoDeColumna Tipo = TipoDeColumna.Texto, string? Clave = null)
{
    /// <summary>
    /// Una columna cuya <see cref="Clave"/> empieza con «_» viaja a la pantalla pero no se pinta ni
    /// se exporta: lleva claves de profundización (el nodo del libro auxiliar, el comprobante que
    /// abre una fila). Desde la feature 009 E2; los exportadores la quitan con <see cref="TablaExportable.SinOcultas"/>.
    /// </summary>
    public bool EsOculta => Clave is not null && Clave.StartsWith('_');
}

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
    public static TablaExportable Vacia(string titulo, string subtitulo, IReadOnlyList<ColumnaExportable> columnas) =>
        new(titulo, subtitulo, columnas, [], null, []);

    /// <summary>La misma tabla sin las columnas ocultas (ni sus valores): lo que se pinta y se exporta.</summary>
    public TablaExportable SinOcultas()
    {
        if (!Columnas.Any(c => c.EsOculta)) return this;
        var visibles = Columnas.Select((c, i) => (c, i)).Where(x => !x.c.EsOculta).Select(x => x.i).ToArray();
        FilaExportable Recortar(FilaExportable f) => f with { Valores = visibles.Select(i => i < f.Valores.Count ? f.Valores[i] : null).ToList() };
        return this with
        {
            Columnas = visibles.Select(i => Columnas[i]).ToList(),
            Filas = Filas.Select(Recortar).ToList(),
            Totales = Totales is null ? null : Recortar(Totales),
        };
    }
}

/// <summary>Una fila: un valor por columna. <see cref="Seccion"/> agrupa (puede ser nula).</summary>
public sealed record FilaExportable(IReadOnlyList<object?> Valores, string? Seccion = null, bool Resaltada = false);
