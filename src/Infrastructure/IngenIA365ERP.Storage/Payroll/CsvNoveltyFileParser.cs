using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using IngenIA365ERP.Application.Payroll.Services;

namespace IngenIA365ERP.Storage.Payroll;

/// <summary>
/// <see cref="INoveltyFileParser"/> con CsvHelper (D-13): separador <c>;</c>, UTF-8 (con o
/// sin BOM), encabezados en español. Números con coma o punto decimal; fechas
/// <c>yyyy-MM-dd</c> o <c>dd/MM/yyyy</c>. No decide nada de negocio: sólo convierte texto en
/// filas tipadas y dice, con fila y columna, qué no pudo leer.
/// </summary>
public sealed class CsvNoveltyFileParser : INoveltyFileParser
{
    public const string ColDocumento = "documento";
    public const string ColConcepto = "concepto";
    public const string ColCantidad = "cantidad";
    public const string ColValor = "valor";
    public const string ColDesde = "desde";
    public const string ColHasta = "hasta";
    public const string ColObservacion = "observacion";

    private static readonly string[] Requeridas = [ColDocumento, ColConcepto];
    private static readonly string[] Todas = [ColDocumento, ColConcepto, ColCantidad, ColValor, ColDesde, ColHasta, ColObservacion];
    private static readonly string[] FormatosFecha = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd"];
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public NoveltyFileParseResult Parse(Stream content)
    {
        var filas = new List<NoveltyFileRow>();
        var errores = new List<NoveltyFileError>();

        using var reader = new StreamReader(content, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => Normalizar(args.Header),
            IgnoreBlankLines = true,
        };
        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            errores.Add(new NoveltyFileError(1, ColDocumento, "El archivo está vacío o no tiene fila de encabezados."));
            return new NoveltyFileParseResult(filas, errores);
        }

        var encabezados = csv.HeaderRecord.Select(Normalizar).ToHashSet();
        foreach (var req in Requeridas.Where(r => !encabezados.Contains(r)))
            errores.Add(new NoveltyFileError(1, req, $"Falta la columna «{req}». Descargue la plantilla y respete sus encabezados."));
        var desconocidas = encabezados.Where(h => !Todas.Contains(h) && h.Length > 0).ToList();
        if (desconocidas.Count > 0)
            errores.Add(new NoveltyFileError(1, string.Join(",", desconocidas), $"Columnas no reconocidas: {string.Join(", ", desconocidas)}."));
        if (errores.Count > 0) return new NoveltyFileParseResult(filas, errores);

        var fila = 1;
        while (csv.Read())
        {
            fila++;
            var documento = Texto(csv, ColDocumento);
            var concepto = Texto(csv, ColConcepto);
            var todoVacio = string.IsNullOrEmpty(documento) && string.IsNullOrEmpty(concepto)
                            && string.IsNullOrEmpty(Texto(csv, ColCantidad)) && string.IsNullOrEmpty(Texto(csv, ColValor));
            if (todoVacio) continue;

            if (string.IsNullOrEmpty(documento)) errores.Add(new NoveltyFileError(fila, ColDocumento, "El documento del empleado es obligatorio."));
            if (string.IsNullOrEmpty(concepto)) errores.Add(new NoveltyFileError(fila, ColConcepto, "El código del concepto es obligatorio."));

            var cantidad = Decimal(csv, ColCantidad, fila, errores);
            var valor = Decimal(csv, ColValor, fila, errores);
            var desde = Fecha(csv, ColDesde, fila, errores);
            var hasta = Fecha(csv, ColHasta, fila, errores);
            var observacion = Texto(csv, ColObservacion);
            if (observacion?.Length > 500) errores.Add(new NoveltyFileError(fila, ColObservacion, "La observación no puede pasar de 500 caracteres."));

            filas.Add(new NoveltyFileRow(fila, documento ?? string.Empty, (concepto ?? string.Empty).ToUpperInvariant(),
                cantidad, valor, desde, hasta, string.IsNullOrEmpty(observacion) ? null : observacion));
        }

        return new NoveltyFileParseResult(filas, errores);
    }

    public byte[] Template()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", Todas));
        sb.AppendLine("1020304050;HEX_NOCTURNA;6;;;;Turno del 12");
        sb.AppendLine("1020304050;BONIF_NO_SALARIAL;;150000;;;Cumplimiento de metas");
        sb.AppendLine("900123456;INCAP_EG;;;2026-03-10;2026-03-14;Incapacidad general");
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Normalizar(string? h) =>
        (h ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("ó", "o").Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ú", "u")
            .Replace("observación", "observacion");

    private static string? Texto(CsvReader csv, string col)
    {
        try { return csv.TryGetField<string>(col, out var v) ? v?.Trim() : null; }
        catch (CsvHelperException) { return null; }
    }

    private static decimal? Decimal(CsvReader csv, string col, int fila, List<NoveltyFileError> errores)
    {
        var t = Texto(csv, col);
        if (string.IsNullOrEmpty(t)) return null;
        var limpio = t.Replace(" ", string.Empty).Replace("$", string.Empty);
        // 1.234,56 (es-CO) o 1234.56 (invariante): decide el último separador.
        var ultimaComa = limpio.LastIndexOf(',');
        var ultimoPunto = limpio.LastIndexOf('.');
        var cultura = ultimaComa > ultimoPunto ? Co : CultureInfo.InvariantCulture;
        if (decimal.TryParse(limpio, NumberStyles.Number, cultura, out var v)) return v;
        errores.Add(new NoveltyFileError(fila, col, $"«{t}» no es un número válido."));
        return null;
    }

    private static DateTime? Fecha(CsvReader csv, string col, int fila, List<NoveltyFileError> errores)
    {
        var t = Texto(csv, col);
        if (string.IsNullOrEmpty(t)) return null;
        if (DateTime.TryParseExact(t, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d.Date;
        errores.Add(new NoveltyFileError(fila, col, $"«{t}» no es una fecha válida (use yyyy-MM-dd o dd/MM/yyyy)."));
        return null;
    }
}
