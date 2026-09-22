using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using IngenIA365ERP.Application.Common.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports.Exportadores;

/// <summary>Un archivo listo para descargar.</summary>
public sealed record ArchivoExportado(byte[] Contenido, string TipoContenido, string NombreArchivo);

/// <summary>
/// Convierte una <see cref="TablaExportable"/> en Excel (ClosedXML), Word (OpenXML SDK, sin
/// Office) o PDF (QuestPDF). Los tres muestran lo mismo que la pantalla: mismas columnas,
/// mismas filas, mismos totales. Los números van como números (Excel suma; Word y PDF
/// formatean con la cultura es-CO).
/// </summary>
public static class ExportadorDeTablas
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public static ArchivoExportado Exportar(TablaExportable tabla, string formato, string nombreBase) => Exportar2(tabla.SinOcultas(), formato, nombreBase);

    private static ArchivoExportado Exportar2(TablaExportable tabla, string formato, string nombreBase) => formato.ToLowerInvariant() switch
    {
        "xlsx" => new(Excel(tabla), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreBase + ".xlsx"),
        "docx" => new(Word(tabla), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", nombreBase + ".docx"),
        "pdf" => new(Pdf(tabla), "application/pdf", nombreBase + ".pdf"),
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formatos: xlsx, docx, pdf."),
    };

    public static string Texto(object? valor, TipoDeColumna tipo) => valor switch
    {
        null => string.Empty,
        decimal d when tipo == TipoDeColumna.Moneda => d.ToString("N0", Cultura),
        decimal d when tipo == TipoDeColumna.Decimal => d.ToString("0.##", Cultura),
        decimal d when tipo == TipoDeColumna.Porcentaje => d.ToString("N2", Cultura) + " %",
        decimal d => d.ToString("N2", Cultura),
        int i => i.ToString(Cultura),
        long l => l.ToString(Cultura),
        DateTime f => tipo == TipoDeColumna.Fecha ? f.ToString("dd/MM/yyyy", Cultura) : f.ToString("dd/MM/yyyy HH:mm", Cultura),
        DateOnly f => f.ToString("dd/MM/yyyy", Cultura),
        bool b => b ? "Sí" : "No",
        _ => valor.ToString() ?? string.Empty,
    };

    private static bool EsNumerica(TipoDeColumna t) => t is TipoDeColumna.Entero or TipoDeColumna.Moneda or TipoDeColumna.Decimal or TipoDeColumna.Porcentaje;

    // ------------------------------------------------------------------ Excel --

    private static byte[] Excel(TablaExportable tabla)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet(Recortar(tabla.Titulo, 31));
        hoja.Cell(1, 1).Value = tabla.Titulo;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;
        hoja.Cell(2, 1).Value = tabla.Subtitulo;
        hoja.Cell(2, 1).Style.Font.Italic = true;

        var fila = 4;
        var conSeccion = tabla.Filas.Any(f => f.Seccion is not null);
        var col = 1;
        if (conSeccion) hoja.Cell(fila, col++).Value = "Sección";
        foreach (var c in tabla.Columnas) hoja.Cell(fila, col++).Value = c.Nombre;
        var encabezado = hoja.Range(fila, 1, fila, col - 1);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2D8EA");
        fila++;

        foreach (var f in tabla.Filas)
        {
            col = 1;
            if (conSeccion) hoja.Cell(fila, col++).Value = f.Seccion ?? string.Empty;
            for (var i = 0; i < tabla.Columnas.Count; i++) Celda(hoja.Cell(fila, col++), f.Valores.ElementAtOrDefault(i), tabla.Columnas[i].Tipo);
            if (f.Resaltada) hoja.Range(fila, 1, fila, col - 1).Style.Font.Bold = true;
            fila++;
        }
        if (tabla.Totales is { } t)
        {
            col = 1;
            if (conSeccion) hoja.Cell(fila, col++).Value = string.Empty;
            for (var i = 0; i < tabla.Columnas.Count; i++) Celda(hoja.Cell(fila, col++), t.Valores.ElementAtOrDefault(i), tabla.Columnas[i].Tipo);
            var rango = hoja.Range(fila, 1, fila, col - 1);
            rango.Style.Font.Bold = true;
            rango.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            fila++;
        }
        fila++;
        foreach (var nota in tabla.Notas) { hoja.Cell(fila++, 1).Value = nota; }
        hoja.Columns().AdjustToContents(4, Math.Max(5, fila));
        if (tabla.HojaPorSeccion) HojasPorSeccion(libro, tabla);
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Feature 010 (FR-012): una hoja más por sección —el fondo de cesantías— con el mismo
    /// encabezado, sólo sus filas y un subtotal de las columnas numéricas. El nombre de la hoja
    /// respeta el límite de 31 caracteres y los que Excel prohíbe; si dos secciones chocan, se numeran.
    /// </summary>
    private static void HojasPorSeccion(XLWorkbook libro, TablaExportable tabla)
    {
        var usados = new HashSet<string>(libro.Worksheets.Select(w => w.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var seccion in tabla.Filas.Where(f => f.Seccion is not null).Select(f => f.Seccion!).Distinct())
        {
            var hoja = libro.AddWorksheet(NombreDeHoja(seccion, usados));
            hoja.Cell(1, 1).Value = tabla.Titulo;
            hoja.Cell(1, 1).Style.Font.Bold = true;
            hoja.Cell(2, 1).Value = seccion;
            hoja.Cell(2, 1).Style.Font.Bold = true;
            hoja.Cell(2, 1).Style.Font.FontSize = 13;
            hoja.Cell(3, 1).Value = tabla.Subtitulo;
            hoja.Cell(3, 1).Style.Font.Italic = true;

            var fila = 5;
            for (var i = 0; i < tabla.Columnas.Count; i++) hoja.Cell(fila, i + 1).Value = tabla.Columnas[i].Nombre;
            var encabezado = hoja.Range(fila, 1, fila, tabla.Columnas.Count);
            encabezado.Style.Font.Bold = true;
            encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2D8EA");
            fila++;

            var filas = tabla.Filas.Where(f => f.Seccion == seccion).ToList();
            foreach (var f in filas)
            {
                for (var i = 0; i < tabla.Columnas.Count; i++) Celda(hoja.Cell(fila, i + 1), f.Valores.ElementAtOrDefault(i), tabla.Columnas[i].Tipo);
                if (f.Resaltada) hoja.Range(fila, 1, fila, tabla.Columnas.Count).Style.Font.Bold = true;
                fila++;
            }

            // Subtotal de la sección: suma de las columnas numéricas de los renglones de detalle (las
            // filas resaltadas son totales que la vista ya trae y no se vuelven a sumar).
            var detalle = filas.Where(f => !f.Resaltada).ToList();
            hoja.Cell(fila, 1).Value = $"Total {seccion}";
            for (var i = 1; i < tabla.Columnas.Count; i++)
            {
                var tipo = tabla.Columnas[i].Tipo;
                if (tipo is not (TipoDeColumna.Moneda or TipoDeColumna.Decimal)) continue;
                var suma = detalle.Select(f => f.Valores.ElementAtOrDefault(i)).OfType<decimal>().Sum();
                Celda(hoja.Cell(fila, i + 1), suma, tipo);
            }
            var total = hoja.Range(fila, 1, fila, tabla.Columnas.Count);
            total.Style.Font.Bold = true;
            total.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            hoja.Cell(fila + 1, 1).Value = $"{detalle.Count} renglón(es)";
            hoja.Columns().AdjustToContents(5, Math.Max(6, fila + 1));
        }
    }

    private static string NombreDeHoja(string seccion, HashSet<string> usados)
    {
        var limpio = new string(seccion.Select(c => @"\/?*[]:".Contains(c) ? ' ' : c).ToArray()).Trim();
        if (limpio.Length == 0) limpio = "Sección";
        var nombre = Recortar(limpio, 31);
        var n = 2;
        while (!usados.Add(nombre))
        {
            var sufijo = $" ({n++})";
            nombre = Recortar(limpio, 31 - sufijo.Length) + sufijo;
        }
        return nombre;
    }

    private static void Celda(IXLCell celda, object? valor, TipoDeColumna tipo)
    {
        switch (valor)
        {
            case null: celda.Value = Blank.Value; break;
            case decimal d:
                celda.Value = d;
                celda.Style.NumberFormat.Format = tipo switch { TipoDeColumna.Moneda => "#,##0", TipoDeColumna.Porcentaje => @"#,##0.00 ""%""", _ => "#,##0.##" };
                break;
            case int i: celda.Value = i; break;
            case long l: celda.Value = l; break;
            case DateTime f:
                celda.Value = f;
                celda.Style.DateFormat.Format = tipo == TipoDeColumna.Fecha ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm";
                break;
            case DateOnly f:
                celda.Value = f.ToDateTime(TimeOnly.MinValue);
                celda.Style.DateFormat.Format = "dd/MM/yyyy";
                break;
            default: celda.Value = valor.ToString(); break;
        }
    }

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];

    // ------------------------------------------------------------------- Word --

    private static byte[] Word(TablaExportable tabla)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new W.Document(new Body());
            var body = main.Document.Body!;
            body.Append(Parrafo(tabla.Titulo, negrita: true, tamano: 28));
            body.Append(Parrafo(tabla.Subtitulo, cursiva: true, tamano: 20));

            var conSeccion = tabla.Filas.Any(f => f.Seccion is not null);
            var t = new Table();
            var props = new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 }, new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 }, new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2 }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 2 }),
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" });
            t.AppendChild(props);

            var encabezado = new TableRow();
            if (conSeccion) encabezado.Append(CeldaWord("Sección", negrita: true, sombreado: true));
            foreach (var c in tabla.Columnas) encabezado.Append(CeldaWord(c.Nombre, negrita: true, sombreado: true, derecha: EsNumerica(c.Tipo)));
            t.Append(encabezado);

            foreach (var f in tabla.Filas)
            {
                var tr = new TableRow();
                if (conSeccion) tr.Append(CeldaWord(f.Seccion ?? string.Empty));
                for (var i = 0; i < tabla.Columnas.Count; i++)
                    tr.Append(CeldaWord(Texto(f.Valores.ElementAtOrDefault(i), tabla.Columnas[i].Tipo), negrita: f.Resaltada, derecha: EsNumerica(tabla.Columnas[i].Tipo)));
                t.Append(tr);
            }
            if (tabla.Totales is { } tot)
            {
                var tr = new TableRow();
                if (conSeccion) tr.Append(CeldaWord(string.Empty, negrita: true));
                for (var i = 0; i < tabla.Columnas.Count; i++)
                    tr.Append(CeldaWord(Texto(tot.Valores.ElementAtOrDefault(i), tabla.Columnas[i].Tipo), negrita: true, derecha: EsNumerica(tabla.Columnas[i].Tipo)));
                t.Append(tr);
            }
            body.Append(t);
            foreach (var nota in tabla.Notas) body.Append(Parrafo(nota, tamano: 18));
            body.Append(new SectionProperties(new W.PageSize { Width = 15840, Height = 12240, Orient = PageOrientationValues.Landscape },
                new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720 }));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static Paragraph Parrafo(string texto, bool negrita = false, bool cursiva = false, int tamano = 22)
    {
        var rp = new RunProperties(new FontSize { Val = tamano.ToString() });
        if (negrita) rp.Append(new Bold());
        if (cursiva) rp.Append(new Italic());
        return new Paragraph(new Run(rp, new Text(texto) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static TableCell CeldaWord(string texto, bool negrita = false, bool sombreado = false, bool derecha = false)
    {
        var rp = new RunProperties(new FontSize { Val = "18" });
        if (negrita) rp.Append(new Bold());
        var pp = new ParagraphProperties(new Justification { Val = derecha ? JustificationValues.Right : JustificationValues.Left });
        var celda = new TableCell(new Paragraph(pp, new Run(rp, new Text(texto) { Space = SpaceProcessingModeValues.Preserve })));
        // tcPr va ANTES del párrafo: en otro orden Word declara el archivo dañado.
        if (sombreado) celda.InsertAt(new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = "E2D8EA" }), 0);
        return celda;
    }

    // -------------------------------------------------------------------- PDF --

    private static byte[] Pdf(TablaExportable tabla)
    {
        var conSeccion = tabla.Filas.Any(f => f.Seccion is not null);
        var documento = QuestPDF.Fluent.Document.Create(c => c.Page(page =>
        {
            page.Size(tabla.Columnas.Count > 7 ? PageSizes.Letter.Landscape() : PageSizes.Letter);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(8));
            page.Header().Column(h =>
            {
                h.Item().Text(tabla.Titulo).FontSize(14).Bold();
                h.Item().Text(tabla.Subtitulo).FontSize(9).Italic();
                h.Item().PaddingBottom(6);
            });
            page.Content().Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    if (conSeccion) cols.ConstantColumn(70);
                    foreach (var col in tabla.Columnas) cols.RelativeColumn(EsNumerica(col.Tipo) ? 2 : col.Tipo == TipoDeColumna.Fecha ? 2 : 3);
                });
                t.Header(h =>
                {
                    if (conSeccion) h.Cell().Element(Encabezado).Text("Sección");
                    foreach (var col in tabla.Columnas)
                        h.Cell().Element(Encabezado).AlignRight(EsNumerica(col.Tipo)).Text(col.Nombre).Bold();
                });
                string? seccionAnterior = null;
                foreach (var f in tabla.Filas)
                {
                    if (conSeccion)
                    {
                        t.Cell().Element(CeldaPdf).Text(f.Seccion == seccionAnterior ? string.Empty : f.Seccion ?? string.Empty).Bold();
                        seccionAnterior = f.Seccion;
                    }
                    for (var i = 0; i < tabla.Columnas.Count; i++)
                    {
                        var texto = Texto(f.Valores.ElementAtOrDefault(i), tabla.Columnas[i].Tipo);
                        var celda = t.Cell().Element(CeldaPdf).AlignRight(EsNumerica(tabla.Columnas[i].Tipo));
                        if (f.Resaltada) celda.Text(texto).Bold(); else celda.Text(texto);
                    }
                }
                if (tabla.Totales is { } tot)
                {
                    if (conSeccion) t.Cell().Element(CeldaTotal).Text(string.Empty);
                    for (var i = 0; i < tabla.Columnas.Count; i++)
                        t.Cell().Element(CeldaTotal).AlignRight(EsNumerica(tabla.Columnas[i].Tipo)).Text(Texto(tot.Valores.ElementAtOrDefault(i), tabla.Columnas[i].Tipo)).Bold();
                }
            });
            page.Footer().Column(f =>
            {
                foreach (var nota in tabla.Notas) f.Item().Text(nota).FontSize(7);
                f.Item().AlignRight().Text(x => { x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
            });
        }));
        return documento.GeneratePdf();
    }

    private static IContainer Encabezado(IContainer c) => c.Background("#E2D8EA").Padding(3).BorderBottom(1).BorderColor(Colors.Grey.Medium);
    private static IContainer CeldaPdf(IContainer c) => c.Padding(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
    private static IContainer CeldaTotal(IContainer c) => c.Padding(3).BorderTop(1).BorderColor(Colors.Grey.Medium);
    private static IContainer AlignRight(this IContainer c, bool derecha) => derecha ? c.AlignRight() : c;
}
