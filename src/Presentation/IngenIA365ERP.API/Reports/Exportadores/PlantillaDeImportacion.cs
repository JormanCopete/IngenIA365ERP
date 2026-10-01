using ClosedXML.Excel;
using IngenIA365ERP.API.Reports.Importadores;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Reports;

namespace IngenIA365ERP.API.Reports.Exportadores;

/// <summary>
/// Una plantilla para llenar y volver a importar (feature 009 E2, US13: los saldos de apertura).
/// A diferencia de <see cref="ExportadorDeTablas"/>, que arma un informe —título, subtítulo,
/// encabezados en la fila 4, notas al pie—, aquí la primera hoja lleva <b>sólo los encabezados en
/// la fila 1</b>, que es lo que <c>ClosedXmlTabularFileReader</c> lee con una fila de encabezado;
/// título, subtítulo y notas van en una segunda hoja «Instrucciones» que el lector no mira.
///
/// <para>
/// Feature 012 (T49, T158; contracts/plantillas.md §0.3, §0.5, §0.6): libros de <b>varias hojas de datos</b> a partir de
/// una <see cref="DefinicionDePlantilla"/> (cada hoja con sus encabezados en la fila 1, texto <c>@</c> en códigos y
/// documentos, <c>0.00</c> en montos, <c>0.0000</c> en cantidades y porcentajes, <c>0.000000</c> en costos y factores),
/// la hoja «Instrucciones» con una línea por columna (tipo, obligatoria, reglas, ejemplo), la descarga con datos
/// (<see cref="DatosDePlantilla"/>) y el libro de la revisión con las columnas <c>resultado</c> y <c>errores</c>.
/// </para>
/// </summary>
public static class PlantillaDeImportacion
{
    public const string TipoContenido = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public const string HojaDeInstrucciones = "Instrucciones";
    public const string ColumnaResultado = "resultado";
    public const string ColumnaErrores = "errores";

    private static readonly XLColor FondoDeEncabezado = XLColor.FromHtml("#E8EEF7");

    public static ArchivoExportado Xlsx(TablaExportable tabla, string nombreBase)
    {
        using var libro = new XLWorkbook();
        var datos = libro.AddWorksheet("Datos");
        var columnas = tabla.SinOcultas().Columnas;
        for (var i = 0; i < columnas.Count; i++)
        {
            Encabezado(datos.Cell(1, i + 1), columnas[i].Nombre);
            datos.Column(i + 1).Style.NumberFormat.Format = columnas[i].Tipo switch
            {
                TipoDeColumna.Moneda or TipoDeColumna.Decimal => "0.00",
                TipoDeColumna.Cantidad => "0.0000",
                TipoDeColumna.Costo => "0.000000",
                _ => "@",
            };
            datos.Column(i + 1).Width = Math.Max(14, columnas[i].Nombre.Length + 4);
        }
        datos.SheetView.FreezeRows(1);

        var instrucciones = libro.AddWorksheet(HojaDeInstrucciones);
        instrucciones.Cell(1, 1).Value = tabla.Titulo;
        instrucciones.Cell(1, 1).Style.Font.Bold = true;
        instrucciones.Cell(1, 1).Style.Font.FontSize = 14;
        instrucciones.Cell(2, 1).Value = tabla.Subtitulo;
        instrucciones.Cell(2, 1).Style.Font.Italic = true;
        var fila = 4;
        foreach (var nota in tabla.Notas) instrucciones.Cell(fila++, 1).Value = nota;
        instrucciones.Column(1).Width = 120;

        return Guardar(libro, nombreBase + ".xlsx");
    }

    // ------------------------------------------------------- varias hojas (012) --

    /// <summary>
    /// El libro de una plantilla: una hoja de datos por sección con sus encabezados en la fila 1 y, con
    /// <paramref name="datos"/>, llena con lo que hoy tiene la cooperativa; al final, «Instrucciones».
    /// </summary>
    public static ArchivoExportado Xlsx(DefinicionDePlantilla plantilla, string nombreBase, DatosDePlantilla? datos = null)
    {
        using var libro = new XLWorkbook();
        foreach (var hoja in plantilla.Hojas)
        {
            var ws = libro.AddWorksheet(NombreDeHoja(hoja.Nombre));
            for (var i = 0; i < hoja.Columnas.Count; i++)
            {
                var columna = hoja.Columnas[i];
                Encabezado(ws.Cell(1, i + 1), columna.Nombre);
                ws.Column(i + 1).Style.NumberFormat.Format = Formato(columna.Tipo);
                ws.Column(i + 1).Width = Math.Max(14, columna.Nombre.Length + 4);
            }

            if (datos is not null)
            {
                var fila = 2;
                foreach (var valores in datos.De(hoja.Nombre))
                {
                    for (var i = 0; i < hoja.Columnas.Count && i < valores.Count; i++)
                        Valor(ws.Cell(fila, i + 1), valores[i], hoja.Columnas[i].Tipo);
                    fila++;
                }
            }
            ws.SheetView.FreezeRows(1);
        }

        Instrucciones(libro.AddWorksheet(HojaDeInstrucciones), plantilla);
        return Guardar(libro, nombreBase + ".xlsx");
    }

    /// <summary>
    /// El mismo libro que se subió con dos columnas al final de cada hoja de datos, <c>resultado</c> (Crear, Actualizar,
    /// Sin cambio) y <c>errores</c>, para corregir sobre el mismo archivo (§0.5, <c>format=xlsx</c> en la revisión). Un
    /// <c>.csv</c> vuelve como libro con su hoja «Datos».
    /// </summary>
    public static ArchivoExportado ConResultados(ArchivoDeImportacion archivo, DefinicionDePlantilla plantilla, ImportResultDto resultado)
    {
        using var libro = AbrirOConvertir(archivo);
        var porFila = resultado.Rows
            .GroupBy(r => (Hoja: TablaLeida.Normalizar(r.Sheet), r.Row))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var hoja in plantilla.Hojas)
        {
            var ws = plantilla.EsDeUnaHoja
                ? libro.Worksheets.FirstOrDefault(w => w.Name != HojaDeInstrucciones)
                : libro.Worksheets.FirstOrDefault(w => TablaLeida.Normalizar(w.Name) == TablaLeida.Normalizar(hoja.Nombre));
            if (ws is null) continue;

            var usado = ws.RangeUsed();
            var ultimaColumna = usado?.LastColumn().ColumnNumber() ?? 0;
            // Si el libro ya traía las dos columnas (se revisa el libro de una revisión anterior), se reescriben.
            var colResultado = BuscarColumna(ws, ColumnaResultado, ultimaColumna) ?? ++ultimaColumna;
            var colErrores = BuscarColumna(ws, ColumnaErrores, ultimaColumna) ?? ++ultimaColumna;
            Encabezado(ws.Cell(1, colResultado), ColumnaResultado);
            Encabezado(ws.Cell(1, colErrores), ColumnaErrores);

            var ultimaFila = usado?.LastRow().RowNumber() ?? 1;
            for (var fila = 2; fila <= ultimaFila; fila++)
            {
                ws.Cell(fila, colResultado).Value = Blank.Value;
                ws.Cell(fila, colErrores).Value = Blank.Value;
                if (!porFila.TryGetValue((TablaLeida.Normalizar(hoja.Nombre), fila), out var r)) continue;
                ws.Cell(fila, colResultado).Value = r.Action switch
                {
                    AccionDeImportacion.Create => "Crear",
                    AccionDeImportacion.Update => "Actualizar",
                    AccionDeImportacion.Unchanged => "Sin cambio",
                    _ => r.Errors.Count > 0 ? "Con errores" : string.Empty,
                };
                if (r.Errors.Count > 0)
                {
                    ws.Cell(fila, colErrores).Value = string.Join(" | ", r.Errors);
                    ws.Cell(fila, colErrores).Style.Font.FontColor = XLColor.DarkRed;
                }
            }
            ws.Column(colResultado).Width = 14;
            ws.Column(colErrores).Width = 80;
        }

        var nombre = Path.GetFileNameWithoutExtension(archivo.NombreArchivo);
        return Guardar(libro, (string.IsNullOrWhiteSpace(nombre) ? plantilla.Clave : nombre) + "-revision.xlsx");
    }

    /// <summary>El formato de celda por tipo de §0.3.</summary>
    public static string Formato(TipoDeValor tipo) => tipo switch
    {
        TipoDeValor.Monto => "0.00",
        TipoDeValor.Cantidad => "0.0000",
        TipoDeValor.Porcentaje => "0.0000",
        TipoDeValor.Costo => "0.000000",
        TipoDeValor.Entero => "0",
        _ => "@",
    };

    // ------------------------------------------------------------- auxiliares --

    private static void Instrucciones(IXLWorksheet ws, DefinicionDePlantilla plantilla)
    {
        ws.Cell(1, 1).Value = plantilla.Nombre;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = plantilla.EsDeUnaHoja
            ? "Llene la hoja de datos desde la fila 2; no cambie los encabezados de la fila 1. También se acepta .csv."
            : "Llene cada hoja desde la fila 2; no cambie los nombres de las hojas ni los encabezados de la fila 1.";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(3, 1).Value = "Primero revise el archivo (mode=review): no se guarda nada. Al aplicar, con un solo error no se guarda nada. "
            + "El código identifica la fila: si ya existe, se actualiza; la plantilla nunca borra. Esta hoja no se lee.";

        var fila = 5;
        foreach (var hoja in plantilla.Hojas)
        {
            ws.Cell(fila, 1).Value = $"Hoja «{hoja.Nombre}»" + (hoja.Obligatoria ? string.Empty : " (opcional)")
                + (hoja.Permiso is null ? string.Empty : $" — exige el permiso {hoja.Permiso}");
            ws.Cell(fila, 1).Style.Font.Bold = true;
            fila++;
            string[] titulos = ["columna", "tipo", "obligatoria", "reglas", "ejemplo"];
            for (var i = 0; i < titulos.Length; i++) Encabezado(ws.Cell(fila, i + 1), titulos[i]);
            fila++;
            foreach (var columna in hoja.Columnas)
            {
                ws.Cell(fila, 1).Value = columna.Nombre;
                ws.Cell(fila, 2).Value = Tipo(columna);
                ws.Cell(fila, 3).Value = columna.Obligatoria ? "sí" : "no";
                var reglas = columna.Reglas ?? string.Empty;
                if (columna.Permiso is not null) reglas = (reglas.Length > 0 ? reglas + " " : string.Empty) + $"Exige el permiso {columna.Permiso}.";
                ws.Cell(fila, 4).Value = reglas;
                ws.Cell(fila, 5).Value = columna.Ejemplo ?? string.Empty;
                ws.Cell(fila, 5).Style.NumberFormat.Format = "@";
                fila++;
            }
            fila++;
        }
        ws.Column(1).Width = 28;
        ws.Column(2).Width = 22;
        ws.Column(3).Width = 12;
        ws.Column(4).Width = 90;
        ws.Column(5).Width = 22;
    }

    private static string Tipo(ColumnaDePlantilla c) => c.Tipo switch
    {
        TipoDeValor.Codigo => $"código {c.Largo ?? 10}",
        TipoDeValor.Texto => c.Largo is { } l ? $"texto {l}" : "texto",
        TipoDeValor.Entero => "entero",
        TipoDeValor.Monto => "monto (2 decimales)",
        TipoDeValor.Cantidad => "cantidad (4 decimales)",
        TipoDeValor.Costo => "costo o factor (6 decimales)",
        TipoDeValor.Porcentaje => "porcentaje en puntos (19 = 19 %)",
        TipoDeValor.Fecha => "fecha AAAA-MM-DD",
        TipoDeValor.Hora => "hora HH:mm",
        TipoDeValor.SiNo => "sí/no",
        TipoDeValor.SiNoIndiferente => "sí/no (vacío = no importa)",
        TipoDeValor.Lista => "lista separada por comas (* = todos)",
        TipoDeValor.Enumeracion => "valor de la lista",
        TipoDeValor.Persona => "documento de la persona",
        TipoDeValor.Sucursal => "sucursal (código o nombre)",
        TipoDeValor.CentroDeCosto => "centro de costo (código o nombre)",
        TipoDeValor.Banco => "banco (nombre o código)",
        TipoDeValor.CuentaContable => "cuenta auxiliar",
        _ => c.Tipo.ToString(),
    };

    private static void Valor(IXLCell celda, object? valor, TipoDeValor tipo)
    {
        switch (valor)
        {
            case null: celda.Value = Blank.Value; break;
            // Un porcentaje se guarda como fracción (0,19) y la plantilla lo pide en puntos (19).
            case decimal d when tipo == TipoDeValor.Porcentaje: celda.Value = d * 100m; break;
            case decimal d: celda.Value = d; break;
            case int i: celda.Value = i; break;
            case long l: celda.Value = l; break;
            case bool b: celda.Value = b ? "sí" : "no"; break;
            case DateOnly f: celda.Value = f.ToString("yyyy-MM-dd"); break;
            case DateTime f: celda.Value = f.ToString("yyyy-MM-dd"); break;
            case TimeOnly h: celda.Value = h.ToString("HH:mm"); break;
            case IEnumerable<string> lista: celda.Value = string.Join(", ", lista); break;
            default: celda.Value = valor.ToString(); break;
        }
    }

    private static void Encabezado(IXLCell celda, string texto)
    {
        celda.Value = texto;
        celda.Style.Font.Bold = true;
        celda.Style.Fill.BackgroundColor = FondoDeEncabezado;
    }

    private static int? BuscarColumna(IXLWorksheet ws, string encabezado, int ultima)
    {
        for (var c = 1; c <= ultima; c++)
            if (TablaLeida.Normalizar(ws.Cell(1, c).GetString()) == TablaLeida.Normalizar(encabezado)) return c;
        return null;
    }

    private static XLWorkbook AbrirOConvertir(ArchivoDeImportacion archivo)
    {
        var extension = Path.GetExtension(archivo.NombreArchivo ?? string.Empty).ToLowerInvariant();
        var esLibro = extension is ".xlsx" or ".xlsm"
            || (extension is not (".csv" or ".txt" or ".tsv") && archivo.Contenido.Length > 3 && archivo.Contenido[0] == 0x50 && archivo.Contenido[1] == 0x4B);
        if (esLibro) return new XLWorkbook(new MemoryStream(archivo.Contenido));

        // Un .csv: se arma el libro con sus celdas tal como se leyeron, en la hoja «Datos».
        var libro = new XLWorkbook();
        var ws = libro.AddWorksheet(ArchivosTabulares.HojaDeTexto);
        var leida = new ClosedXmlTabularFileReader().LeerAsync(archivo.Contenido, archivo.NombreArchivo ?? "archivo.csv").GetAwaiter().GetResult();
        if (leida.IsFailure) return libro;
        for (var i = 0; i < leida.Value.Encabezados.Count; i++) ws.Cell(1, i + 1).Value = leida.Value.Encabezados[i];
        foreach (var fila in leida.Value.Filas)
            for (var i = 0; i < fila.Celdas.Count; i++)
            {
                ws.Cell(fila.Numero, i + 1).Style.NumberFormat.Format = "@";
                ws.Cell(fila.Numero, i + 1).Value = fila.Celdas[i];
            }
        return libro;
    }

    private static string NombreDeHoja(string nombre) => nombre.Length <= 31 ? nombre : nombre[..31];

    private static ArchivoExportado Guardar(XLWorkbook libro, string nombreArchivo)
    {
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return new ArchivoExportado(ms.ToArray(), TipoContenido, nombreArchivo);
    }
}
