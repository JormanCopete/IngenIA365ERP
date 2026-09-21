using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Domain.Payroll.Pila;

/// <summary>
/// Proyecta la cabecera y las líneas al layout (feature 010, US5; contracts/archivos.md §1.1):
/// ancho fijo, N a la derecha con ceros, A a la izquierda con espacios, mayúsculas, sin
/// tildes y con Ñ → N, tarifas con decimales fijos, fechas <c>AAAA-MM-DD</c>, períodos
/// <c>AAAA-MM</c>, CRLF y ASCII. No sabe de nómina: pone un valor en una posición.
/// </summary>
public static class PilaWriter
{
    public sealed record Output(string HeaderText, IReadOnlyList<string> LineTexts, byte[] Content, string Text, IReadOnlyList<Dictionary<int, string>> LineFields);

    public static Output Write(PilaLayout layout, PilaResult result)
    {
        var fin = layout.LineEnding.Equals("LF", StringComparison.OrdinalIgnoreCase) ? "\n" : "\r\n";
        var cabecera = Registro(layout.Type1, result.HeaderValues, out _);
        var lineas = new List<string>(result.Lines.Count);
        var campos = new List<Dictionary<int, string>>(result.Lines.Count);
        foreach (var l in result.Lines)
        {
            lineas.Add(Registro(layout.Type2, l.Values, out var porCampo));
            campos.Add(porCampo);
        }
        var sb = new StringBuilder();
        sb.Append(cabecera).Append(fin);
        foreach (var l in lineas) sb.Append(l).Append(fin);
        var texto = sb.ToString();
        var encoding = Encoding.GetEncoding(layout.Encoding);
        return new Output(cabecera, lineas, encoding.GetBytes(texto), texto, campos);
    }

    /// <summary>Un registro completo: cada campo formateado y en su posición.</summary>
    public static string Registro(PilaRecordLayout record, IReadOnlyDictionary<int, object?> values, out Dictionary<int, string> porCampo)
    {
        porCampo = [];
        var sb = new StringBuilder(record.Length);
        foreach (var f in record.Fields.OrderBy(f => f.Number))
        {
            values.TryGetValue(f.Number, out var valor);
            var texto = Formatear(f, f.Source == "Constant" && valor is null ? f.Constant : valor);
            porCampo[f.Number] = texto;
            sb.Append(texto);
        }
        return sb.ToString();
    }

    /// <summary>El valor de un campo en su largo, con la alineación y el relleno de su tipo.</summary>
    public static string Formatear(PilaFieldLayout f, object? valor)
    {
        var crudo = Crudo(f, valor);
        if (f.Kind == "N")
        {
            // Numérico: derecha con ceros; vacío = ceros (un blanco no cabe en un campo N).
            if (crudo.Length > f.Length) crudo = crudo[^f.Length..];
            return crudo.PadLeft(f.Length, '0');
        }
        crudo = Ascii(crudo.ToUpperInvariant());
        if (crudo.Length > f.Length) crudo = crudo[..f.Length];
        return crudo.PadRight(f.Length, ' ');
    }

    private static string Crudo(PilaFieldLayout f, object? valor)
    {
        if (valor is null) return string.Empty;
        switch (f.Format)
        {
            case "Flag":
                return valor is bool b ? (b ? "X" : string.Empty) : valor.ToString() ?? string.Empty;
            case "Date":
                return valor is DateTime d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : valor is DateOnly dOnly ? dOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : valor.ToString() ?? string.Empty;
            case "Period":
                return valor is DateTime pd ? pd.ToString("yyyy-MM", CultureInfo.InvariantCulture) : valor.ToString() ?? string.Empty;
            case "Rate7":
                return Tarifa(valor, 5);
            case "Rate9":
                return Tarifa(valor, 7);
            case "Integer":
                return valor switch
                {
                    decimal dec => decimal.Truncate(dec).ToString("0", CultureInfo.InvariantCulture),
                    double dbl => Math.Truncate(dbl).ToString("0", CultureInfo.InvariantCulture),
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    long l => l.ToString(CultureInfo.InvariantCulture),
                    bool bi => bi ? "1" : "0",
                    _ => valor.ToString()?.Trim() ?? string.Empty,
                };
            default:
                return valor is bool bt ? (bt ? "X" : string.Empty) : valor.ToString()?.Trim() ?? string.Empty;
        }
    }

    /// <summary>Tarifa como fracción con decimales fijos (<c>0.04000</c>); el anexo no fija los decimales: son del layout.</summary>
    private static string Tarifa(object valor, int decimales)
    {
        var fraccion = valor switch { decimal d => d, double db => (decimal)db, int i => i, _ => decimal.TryParse(valor.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : 0m };
        return fraccion.ToString("0." + new string('0', decimales), CultureInfo.InvariantCulture);
    }

    /// <summary>Sin tildes ni diéresis, Ñ → N, y todo lo que no sea ASCII imprimible se cae.</summary>
    public static string Ascii(string s)
    {
        var normal = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normal.Length);
        foreach (var ch in normal)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch is >= (char)32 and <= (char)126 ? ch : ' ');
        }
        return sb.ToString();
    }
}
