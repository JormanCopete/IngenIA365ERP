using ClosedXML.Excel;
using IngenIA365ERP.Application.Common.Reports;

namespace IngenIA365ERP.API.Reports.Exportadores;

/// <summary>
/// Una plantilla para llenar y volver a importar (feature 009 E2, US13: los saldos de apertura).
/// A diferencia de <see cref="ExportadorDeTablas"/>, que arma un informe —título, subtítulo,
/// encabezados en la fila 4, notas al pie—, aquí la primera hoja lleva <b>sólo los encabezados en
/// la fila 1</b>, que es lo que <c>ClosedXmlTabularFileReader</c> lee con una fila de encabezado;
/// título, subtítulo y notas van en una segunda hoja «Instrucciones» que el lector no mira.
/// </summary>
public static class PlantillaDeImportacion
{
    public const string TipoContenido = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static ArchivoExportado Xlsx(TablaExportable tabla, string nombreBase)
    {
        using var libro = new XLWorkbook();
        var datos = libro.AddWorksheet("Datos");
        var columnas = tabla.SinOcultas().Columnas;
        for (var i = 0; i < columnas.Count; i++)
        {
            var celda = datos.Cell(1, i + 1);
            celda.Value = columnas[i].Nombre;
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF7");
            if (columnas[i].Tipo is TipoDeColumna.Moneda or TipoDeColumna.Decimal)
                datos.Column(i + 1).Style.NumberFormat.Format = "0.00";
            else
                datos.Column(i + 1).Style.NumberFormat.Format = "@";
            datos.Column(i + 1).Width = Math.Max(14, columnas[i].Nombre.Length + 4);
        }
        datos.SheetView.FreezeRows(1);

        var instrucciones = libro.AddWorksheet("Instrucciones");
        instrucciones.Cell(1, 1).Value = tabla.Titulo;
        instrucciones.Cell(1, 1).Style.Font.Bold = true;
        instrucciones.Cell(1, 1).Style.Font.FontSize = 14;
        instrucciones.Cell(2, 1).Value = tabla.Subtitulo;
        instrucciones.Cell(2, 1).Style.Font.Italic = true;
        var fila = 4;
        foreach (var nota in tabla.Notas) instrucciones.Cell(fila++, 1).Value = nota;
        instrucciones.Column(1).Width = 120;

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return new ArchivoExportado(ms.ToArray(), TipoContenido, nombreBase + ".xlsx");
    }
}
