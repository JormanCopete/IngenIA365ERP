using System.Globalization;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Common.BankFiles;

/// <summary>Un problema de un archivo leído: la línea física (1 = la primera del archivo), el campo y qué pasa.</summary>
public sealed record FlatFileReadError(int LineNumber, string? Field, string Message);

/// <summary>
/// Una línea de detalle leída: su número físico en el archivo, el texto tal cual y los valores por origen ya tipados según el campo
/// (<see cref="BankFieldDataType"/>): texto como <see cref="string"/>, entero como <see cref="int"/> (o <see cref="long"/> si no cabe), monto
/// como <see cref="decimal"/> y fecha como <see cref="DateOnly"/>. Un campo vacío es nulo. Las constantes y los blancos no son valores.
/// </summary>
public sealed class FlatFileRow(int lineNumber, string text, IReadOnlyDictionary<BankFieldSource, object?> valores)
{
    public int LineNumber { get; } = lineNumber;

    public string Text { get; } = text;

    public IReadOnlyDictionary<BankFieldSource, object?> Valores { get; } = valores;

    public object? this[BankFieldSource source] => Valores.GetValueOrDefault(source);

    public string? Texto(BankFieldSource source) => this[source] as string;

    public decimal? Monto(BankFieldSource source) => this[source] as decimal?;

    public DateOnly? Fecha(BankFieldSource source) => this[source] as DateOnly?;

    public long? Entero(BankFieldSource source) => this[source] switch { int i => i, long l => l, _ => null };
}

/// <summary>Lo leído: las filas de detalle sin errores, los errores por línea y campo, y cuántas líneas de detalle traía el archivo.</summary>
public sealed record FlatFileReadResult(IReadOnlyList<FlatFileRow> Rows, IReadOnlyList<FlatFileReadError> Errors, int DetailLineCount)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// El lector de archivos planos con el formato común (feature 013, K2, T0830; T78; contracts/adaptadores-y-archivos.md §6.1): el inverso puro
/// de <see cref="FlatFileWriter"/>. Recibe el formato —que es dato— y los bytes, y devuelve una fila por registro de detalle con sus valores
/// por origen y los errores por línea y campo; la cabecera (primera línea) y los totales (última) que el formato declara se saltan. Sirve la
/// vista previa del formato de vuelta (<c>PayrollDeductionResult</c>) y lo reutilizará Recaudo: Cartera no lee la respuesta de la pagaduría,
/// consume <c>ResultadoDeDescuentos</c>. Lo que el escritor no puede deshacer (mayúsculas, tildes quitadas, recortes) se lee como quedó.
/// (nuevo)
/// </summary>
public static class FlatFileReader
{
    public static FlatFileReadResult Leer(BankFileFormat formato, byte[] contenido)
    {
        ArgumentNullException.ThrowIfNull(formato);
        ArgumentNullException.ThrowIfNull(contenido);
        var texto = FlatFileWriter.Codificacion(formato.Encoding).GetString(contenido);
        if (texto.Length > 0 && texto[0] == '﻿') texto = texto[1..];
        var lineas = texto.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        while (lineas.Count > 0 && lineas[^1].Length == 0) lineas.RemoveAt(lineas.Count - 1);

        var desde = formato.HasHeader ? 1 : 0;
        var hasta = formato.HasTrailer ? lineas.Count - 1 : lineas.Count;
        var campos = formato.Fields.Where(f => f.Record == BankFileRecord.Detail && !f.IsDeleted).OrderBy(f => f.Order).ToList();
        var mapas = campos.ToDictionary(f => f, Inverso);

        var filas = new List<FlatFileRow>();
        var errores = new List<FlatFileReadError>();
        var detalle = 0;
        for (var i = desde; i < hasta; i++)
        {
            var numero = i + 1;
            var linea = lineas[i];
            if (linea.Length == 0) continue;
            detalle++;
            var partes = Partir(formato, campos, linea, numero, errores);
            if (partes is null) continue;

            var valores = new Dictionary<BankFieldSource, object?>();
            var conError = false;
            for (var k = 0; k < campos.Count; k++)
            {
                var campo = campos[k];
                if (campo.Source is BankFieldSource.Constant or BankFieldSource.Blank) continue;
                var crudo = Desrellenar(formato, campo, partes[k]);
                if (mapas[campo] is { } inverso && inverso.TryGetValue(crudo, out var original)) crudo = original;
                if (crudo.Length == 0)
                {
                    if (campo.Required)
                    {
                        errores.Add(new FlatFileReadError(numero, campo.Name, $"El campo «{campo.Name}» es obligatorio y viene vacío."));
                        conError = true;
                    }
                    valores[campo.Source] = null;
                    continue;
                }
                if (Tipar(formato, campo, crudo, out var valor)) valores[campo.Source] = valor;
                else
                {
                    errores.Add(new FlatFileReadError(numero, campo.Name, $"«{crudo}» no es {Describir(campo.DataType)} en el campo «{campo.Name}»."));
                    conError = true;
                }
            }
            if (!conError) filas.Add(new FlatFileRow(numero, linea, valores));
        }
        return new FlatFileReadResult(filas, errores, detalle);
    }

    /// <summary>Corta la línea en un texto por campo; nulo con su error si no tiene la forma del formato.</summary>
    private static List<string>? Partir(BankFileFormat formato, IReadOnlyList<BankFileFormatField> campos, string linea, int numero, List<FlatFileReadError> errores)
    {
        if (formato.Kind == BankFileKind.FixedWidth)
        {
            var largo = campos.Sum(c => c.Length ?? 0);
            if (linea.Length < largo)
            {
                errores.Add(new FlatFileReadError(numero, null, $"La línea tiene {linea.Length} posiciones y el formato pide {largo}."));
                return null;
            }
            var partes = new List<string>(campos.Count);
            var pos = 0;
            foreach (var c in campos)
            {
                var n = c.Length ?? 0;
                partes.Add(linea.Substring(pos, n));
                pos += n;
            }
            return partes;
        }

        var separadas = Separar(linea, formato.Delimiter ?? ";", formato.QuoteText);
        if (separadas.Count != campos.Count)
        {
            errores.Add(new FlatFileReadError(numero, null, $"La línea trae {separadas.Count} campos y el formato pide {campos.Count}."));
            return null;
        }
        return separadas;
    }

    /// <summary>Separa un registro delimitado respetando las comillas dobles (con <c>""</c> como comilla escapada) si el formato las usa.</summary>
    private static List<string> Separar(string linea, string separador, bool comillas)
    {
        if (!comillas) return [.. linea.Split(separador)];
        var partes = new List<string>();
        var actual = new StringBuilder();
        var dentro = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (dentro)
            {
                if (c == '"' && i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                else if (c == '"') dentro = false;
                else actual.Append(c);
            }
            else if (c == '"') dentro = true;
            else if (string.CompareOrdinal(linea, i, separador, 0, separador.Length) == 0)
            {
                partes.Add(actual.ToString());
                actual.Clear();
                i += separador.Length - 1;
            }
            else actual.Append(c);
        }
        partes.Add(actual.ToString());
        return partes;
    }

    /// <summary>Quita el relleno del lado que lo puso el escritor (a la izquierda si alinea a la derecha) y los espacios sobrantes.</summary>
    private static string Desrellenar(BankFileFormat formato, BankFileFormatField campo, string crudo)
    {
        if (formato.Kind == BankFileKind.FixedWidth && campo.Length is not null)
        {
            var pad = string.IsNullOrEmpty(campo.PadChar) ? ' ' : campo.PadChar[0];
            if (pad != ' ')
            {
                crudo = campo.Alignment == BankFieldAlignment.Right ? crudo.TrimStart(pad) : crudo.TrimEnd(pad);
                // Un número todo relleno de ceros es cero, no vacío.
                if (crudo.Length == 0 && pad == '0' && campo.DataType is BankFieldDataType.Integer or BankFieldDataType.Amount) crudo = "0";
            }
        }
        return crudo.Trim();
    }

    private static bool Tipar(BankFileFormat formato, BankFileFormatField campo, string crudo, out object? valor)
    {
        valor = null;
        switch (campo.DataType)
        {
            case BankFieldDataType.Integer:
                if (!long.TryParse(crudo, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var entero)) return false;
                valor = entero is >= int.MinValue and <= int.MaxValue ? (int)entero : entero;
                return true;
            case BankFieldDataType.Amount:
            {
                if (!string.IsNullOrEmpty(campo.ValueFormat) || formato.AmountFormat == BankFileAmountFormat.Point2)
                {
                    if (!decimal.TryParse(crudo, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d)) return false;
                    valor = d;
                    return true;
                }
                if (!decimal.TryParse(crudo, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var m)) return false;
                valor = formato.AmountFormat == BankFileAmountFormat.ImplicitCents ? m / 100m : m;
                return true;
            }
            case BankFieldDataType.Date:
            {
                var fmt = campo.ValueFormat ?? "yyyyMMdd";
                if (!DateOnly.TryParseExact(crudo, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)) return false;
                valor = fecha;
                return true;
            }
            default:
                valor = crudo;
                return true;
        }
    }

    /// <summary>El mapa de equivalencias al revés (valor del banco → valor del ERP), sin distinguir mayúsculas.</summary>
    private static Dictionary<string, string>? Inverso(BankFileFormatField campo)
    {
        if (string.IsNullOrEmpty(campo.ValueMapJson)) return null;
        var mapa = JsonSerializer.Deserialize<Dictionary<string, string>>(campo.ValueMapJson);
        if (mapa is null || mapa.Count == 0) return null;
        var inverso = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (erp, banco) in mapa) inverso.TryAdd(banco, erp);
        return inverso;
    }

    private static string Describir(BankFieldDataType tipo) => tipo switch
    {
        BankFieldDataType.Integer => "un número entero",
        BankFieldDataType.Amount => "un monto",
        BankFieldDataType.Date => "una fecha",
        _ => "un texto",
    };
}
