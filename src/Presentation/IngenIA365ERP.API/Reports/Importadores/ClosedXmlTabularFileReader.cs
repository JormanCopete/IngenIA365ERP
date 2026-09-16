using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.API.Reports.Importadores;

/// <summary>
/// <see cref="ITabularFileReader"/> con ClosedXML para <c>.xlsx</c> y BCL para texto separado
/// (<c>.csv</c>, <c>.txt</c>, <c>.tsv</c>; delimitador detectado entre «;», «,», tabulador y
/// «|»; comillas dobles como en CSV). Fechas salen como <c>yyyy-MM-dd</c>, números en cultura
/// invariante: quien consume interpreta con esas dos reglas y no depende de la cultura del
/// servidor. Un archivo que no se puede abrir devuelve <c>Archivo.Ilegible</c>, nunca 500.
/// </summary>
public sealed class ClosedXmlTabularFileReader : ITabularFileReader
{
    private static readonly char[] Delimitadores = [';', ',', '\t', '|'];

    public Task<Result<TablaLeida>> LeerAsync(byte[] contenido, string nombreArchivo, int filasDeEncabezado = 1, CancellationToken ct = default)
    {
        if (contenido is null || contenido.Length == 0)
            return Task.FromResult(Result.Failure<TablaLeida>(ArchivosTabulares.Vacio));
        if (filasDeEncabezado < 0) filasDeEncabezado = 0;

        var extension = Path.GetExtension(nombreArchivo ?? string.Empty).ToLowerInvariant();
        try
        {
            var tabla = extension switch
            {
                ".xlsx" or ".xlsm" => LeerExcel(contenido, filasDeEncabezado),
                ".csv" or ".txt" or ".tsv" => LeerTexto(contenido, filasDeEncabezado),
                _ => EsZip(contenido) ? LeerExcel(contenido, filasDeEncabezado) : LeerTexto(contenido, filasDeEncabezado),
            };
            return Task.FromResult(Result.Success(tabla));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(Result.Failure<TablaLeida>(ArchivosTabulares.Ilegible(ex.Message)));
        }
    }

    private static TablaLeida LeerExcel(byte[] contenido, int filasDeEncabezado)
    {
        using var ms = new MemoryStream(contenido);
        using var libro = new XLWorkbook(ms);
        var hoja = libro.Worksheets.First();
        var usado = hoja.RangeUsed();
        if (usado is null) return new TablaLeida([], [], "xlsx");

        var primeraFila = usado.FirstRow().RowNumber();
        var ultimaFila = usado.LastRow().RowNumber();
        var primeraColumna = usado.FirstColumn().ColumnNumber();
        var ultimaColumna = usado.LastColumn().ColumnNumber();

        var encabezados = new List<string>();
        var filas = new List<FilaLeida>();
        for (var fila = primeraFila; fila <= ultimaFila; fila++)
        {
            var celdas = new List<string?>(ultimaColumna - primeraColumna + 1);
            for (var columna = primeraColumna; columna <= ultimaColumna; columna++)
                celdas.Add(Texto(hoja.Cell(fila, columna)));

            var ordinal = fila - primeraFila + 1;
            if (ordinal <= filasDeEncabezado)
            {
                if (ordinal == filasDeEncabezado)
                    encabezados = celdas.Select(c => c?.Trim() ?? string.Empty).ToList();
                continue;
            }
            if (celdas.All(string.IsNullOrWhiteSpace)) continue;
            filas.Add(new FilaLeida(fila, celdas));
        }
        return new TablaLeida(encabezados, filas, "xlsx");
    }

    private static string? Texto(IXLCell celda)
    {
        var valor = celda.Value;
        switch (valor.Type)
        {
            case XLDataType.Blank:
            case XLDataType.Error:
                return null;
            case XLDataType.Boolean:
                return valor.GetBoolean() ? "true" : "false";
            case XLDataType.Number:
                return valor.GetNumber().ToString("0.############", CultureInfo.InvariantCulture);
            case XLDataType.DateTime:
                var fecha = valor.GetDateTime();
                return fecha.ToString(fecha.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            case XLDataType.TimeSpan:
                return valor.GetTimeSpan().ToString();
            case XLDataType.Text:
                var texto = valor.GetText();
                return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
            default:
                return celda.GetFormattedString();
        }
    }

    private static TablaLeida LeerTexto(byte[] contenido, int filasDeEncabezado)
    {
        var texto = Decodificar(contenido);
        var lineas = texto.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        var delimitador = DetectarDelimitador(lineas);

        var encabezados = new List<string>();
        var filas = new List<FilaLeida>();
        var numero = 0;
        foreach (var linea in lineas)
        {
            numero++;
            if (numero <= filasDeEncabezado)
            {
                if (numero == filasDeEncabezado)
                    encabezados = Partir(linea, delimitador).Select(c => c?.Trim() ?? string.Empty).ToList();
                continue;
            }
            if (string.IsNullOrWhiteSpace(linea)) continue;
            filas.Add(new FilaLeida(numero, Partir(linea, delimitador)));
        }
        return new TablaLeida(encabezados, filas, "csv");
    }

    private static string Decodificar(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            // Extractos de banco y archivos de SOLIDO vienen a veces en Latin-1.
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static char DetectarDelimitador(string[] lineas)
    {
        var muestra = lineas.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;
        return Delimitadores.OrderByDescending(d => muestra.Count(ch => ch == d)).First();
    }

    private static List<string?> Partir(string linea, char delimitador)
    {
        var celdas = new List<string?>();
        var actual = new StringBuilder();
        var entreComillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var ch = linea[i];
            if (ch == '"')
            {
                if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                else entreComillas = !entreComillas;
            }
            else if (ch == delimitador && !entreComillas)
            {
                celdas.Add(Limpiar(actual));
                actual.Clear();
            }
            else actual.Append(ch);
        }
        celdas.Add(Limpiar(actual));
        return celdas;
    }

    private static string? Limpiar(StringBuilder sb)
    {
        var s = sb.ToString().Trim();
        return s.Length == 0 ? null : s;
    }

    private static bool EsZip(byte[] b) => b.Length > 3 && b[0] == 0x50 && b[1] == 0x4B;
}
