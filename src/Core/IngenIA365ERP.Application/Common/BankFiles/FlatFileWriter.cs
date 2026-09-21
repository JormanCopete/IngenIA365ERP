using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Common.BankFiles;

/// <summary>Lo que el archivo comparte en todos sus registros: empresa, cuenta origen, fechas, lote.</summary>
public sealed class BankFileContext
{
    public string CompanyNit { get; init; } = string.Empty;
    public string? CompanyNitDv { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? SourceAccountNumber { get; init; }
    public string? SourceAccountType { get; init; }
    public string? SourceBankCode { get; init; }
    public string? SourceAgreementCode { get; init; }
    public DateOnly PaymentDate { get; init; }
    public DateTime GeneratedAt { get; init; }
    public int Sequence { get; init; }
    public string? BatchReference { get; init; }
    public int? Year { get; init; }
    public string? FundNit { get; init; }
    public string? FundPilaCode { get; init; }
}

/// <summary>Los valores de un beneficiario para una línea de detalle, por origen; el caller decide qué va en cada uno.</summary>
public sealed class BankFileLineValues
{
    public Dictionary<BankFieldSource, object?> Valores { get; } = [];
    public object? this[BankFieldSource source]
    {
        get => Valores.GetValueOrDefault(source);
        set => Valores[source] = value;
    }
    public decimal Amount => Valores.TryGetValue(BankFieldSource.Amount, out var v) && v is decimal d ? d : 0m;
}

public sealed record BankFileOutput(byte[] Content, string FileName, string ContentType, string Sha256, IReadOnlyList<string> DetailTexts, int LineCount, decimal TotalAmount, string Text);

/// <summary>Una línea que no cabe en el formato: cuál y en qué campo.</summary>
public sealed class BankFileWriteException(int lineNumber, string field, string message) : Exception(message)
{
    public int LineNumber { get; } = lineNumber;
    public string Field { get; } = field;
}

/// <summary>
/// El único escritor de archivos planos bancarios (feature 010, N4; D-42). Es puro: recibe el
/// formato, el contexto y las líneas ya resueltas por el módulo que paga (nómina hoy; tesorería
/// y contabilidad después) y devuelve los bytes. Sólo sabe poner un valor en una posición con
/// una alineación y un relleno; qué va en cada campo lo dice el formato, que es dato.
/// </summary>
public static class FlatFileWriter
{
    private static readonly Regex Token = new(@"\{(?<src>[A-Za-z]+)(?::(?<fmt>[^}]+))?\}", RegexOptions.Compiled);
    private static bool _codePagesRegistrados;

    /// <summary>Los campos requeridos del detalle que esta línea no puede llenar (para excluirla con motivo antes de escribir).</summary>
    public static IReadOnlyList<string> RequeridosVacios(BankFileFormat formato, BankFileLineValues linea, BankFileContext ctx)
    {
        var faltan = new List<string>();
        foreach (var f in Campos(formato, BankFileRecord.Detail))
        {
            if (!f.Required) continue;
            var valor = Resolver(f, ctx, linea, lineNumber: 1, lineCount: 1, total: 0m);
            if (valor is null || (valor is string s && string.IsNullOrWhiteSpace(s))) faltan.Add(f.Name);
        }
        return faltan;
    }

    public static BankFileOutput Escribir(BankFileFormat formato, BankFileContext ctx, IReadOnlyList<BankFileLineValues> lineas)
    {
        var total = lineas.Sum(l => l.Amount);
        var detalle = new List<string>(lineas.Count);
        var registros = new List<string>();

        if (formato.HasHeader)
            registros.Add(Registro(formato, BankFileRecord.Header, ctx, null, 0, lineas.Count, total));

        var n = 0;
        foreach (var linea in lineas)
        {
            n++;
            var texto = Registro(formato, BankFileRecord.Detail, ctx, linea, n, lineas.Count, total);
            detalle.Add(texto);
            registros.Add(texto);
        }

        if (formato.HasTrailer)
            registros.Add(Registro(formato, BankFileRecord.Trailer, ctx, null, 0, lineas.Count, total));

        var fin = formato.LineEnding == BankFileLineEnding.Lf ? "\n" : "\r\n";
        var contenido = string.Concat(registros.Select(r => r + fin));
        var bytes = Codificacion(formato.Encoding).GetBytes(contenido);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var nombre = NombreDeArchivo(formato, ctx, lineas.Count);
        return new BankFileOutput(bytes, nombre, formato.ContentType, sha, detalle, lineas.Count, total, contenido);
    }

    public static string NombreDeArchivo(BankFileFormat formato, BankFileContext ctx, int lineCount)
    {
        var nombre = Token.Replace(formato.FileNamePattern, m =>
        {
            var src = BankFileFormatDefinition.ParseSource(m.Groups["src"].Value);
            if (src is null) return m.Value;
            var fmt = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : null;
            var valor = Resolver(new BankFileFormatField { Source = src.Value, ValueFormat = fmt, DataType = BankFileFormatDefinition.TipoPorDefecto(src.Value) }, ctx, null, 0, lineCount, 0m);
            return valor switch
            {
                null => string.Empty,
                DateOnly d => d.ToString(fmt ?? "yyyyMMdd", CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString(fmt ?? "yyyyMMdd", CultureInfo.InvariantCulture),
                int i => i.ToString(fmt ?? "0", CultureInfo.InvariantCulture),
                decimal dec => dec.ToString(fmt ?? "0", CultureInfo.InvariantCulture),
                _ => Texto(valor.ToString()!, formato),
            };
        });
        return string.IsNullOrWhiteSpace(nombre) ? "archivo.txt" : nombre;
    }

    // ------------------------------------------------------------------ registros --

    private static IEnumerable<BankFileFormatField> Campos(BankFileFormat formato, BankFileRecord registro) =>
        formato.Fields.Where(f => f.Record == registro && !f.IsDeleted).OrderBy(f => f.Order);

    private static string Registro(BankFileFormat formato, BankFileRecord registro, BankFileContext ctx, BankFileLineValues? linea, int lineNumber, int lineCount, decimal total)
    {
        var partes = new List<string>();
        foreach (var f in Campos(formato, registro))
        {
            var valor = Resolver(f, ctx, linea, lineNumber, lineCount, total);
            var texto = Formatear(f, valor, formato);
            texto = Mapear(f, texto);
            partes.Add(Ajustar(formato, f, texto, lineNumber));
        }
        if (formato.Kind == BankFileKind.FixedWidth) return string.Concat(partes);
        var sep = formato.Delimiter ?? ";";
        return string.Join(sep, partes.Select((p, i) => formato.QuoteText && EsTexto(Campos(formato, registro).ElementAt(i)) ? $"\"{p.Replace("\"", "\"\"")}\"" : p));
    }

    private static bool EsTexto(BankFileFormatField f) => f.DataType == BankFieldDataType.Text;

    private static object? Resolver(BankFileFormatField f, BankFileContext ctx, BankFileLineValues? linea, int lineNumber, int lineCount, decimal total) => f.Source switch
    {
        BankFieldSource.Constant => f.ConstantValue,
        BankFieldSource.Blank => string.Empty,
        BankFieldSource.CompanyNit => ctx.CompanyNit,
        BankFieldSource.CompanyNitDv => ctx.CompanyNitDv,
        BankFieldSource.CompanyName => ctx.CompanyName,
        BankFieldSource.SourceAccountNumber => ctx.SourceAccountNumber,
        BankFieldSource.SourceAccountType => ctx.SourceAccountType,
        BankFieldSource.SourceBankCode => ctx.SourceBankCode,
        BankFieldSource.SourceAgreementCode => ctx.SourceAgreementCode,
        BankFieldSource.PaymentDate => ctx.PaymentDate,
        BankFieldSource.GenerationDate => DateOnly.FromDateTime(ctx.GeneratedAt),
        BankFieldSource.GenerationTime => ctx.GeneratedAt.ToString(f.ValueFormat ?? "HHmmss", CultureInfo.InvariantCulture),
        BankFieldSource.Sequence => ctx.Sequence,
        BankFieldSource.BatchReference => ctx.BatchReference,
        BankFieldSource.LineNumber => lineNumber,
        BankFieldSource.LineCount => lineCount,
        BankFieldSource.TotalAmount => total,
        BankFieldSource.Year => ctx.Year ?? ctx.PaymentDate.Year,
        BankFieldSource.FundNit => ctx.FundNit,
        BankFieldSource.FundPilaCode => ctx.FundPilaCode,
        _ => linea?[f.Source],
    };

    private static string Formatear(BankFileFormatField f, object? valor, BankFileFormat formato)
    {
        if (valor is null) return string.Empty;
        switch (f.DataType)
        {
            case BankFieldDataType.Amount:
            {
                var monto = valor is decimal d ? d : decimal.TryParse(valor.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : 0m;
                if (!string.IsNullOrEmpty(f.ValueFormat)) return monto.ToString(f.ValueFormat, CultureInfo.InvariantCulture);
                return formato.AmountFormat switch
                {
                    BankFileAmountFormat.ImplicitCents => decimal.Round(monto * 100m, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture),
                    BankFileAmountFormat.Point2 => decimal.Round(monto, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture),
                    _ => decimal.Round(monto, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture),
                };
            }
            case BankFieldDataType.Date:
            {
                var fmt = f.ValueFormat ?? "yyyyMMdd";
                return valor switch
                {
                    DateOnly d => d.ToString(fmt, CultureInfo.InvariantCulture),
                    DateTime dt => dt.ToString(fmt, CultureInfo.InvariantCulture),
                    _ => valor.ToString() ?? string.Empty,
                };
            }
            case BankFieldDataType.Integer:
            {
                var entero = valor switch { int i => i, long l => l, decimal d => (long)decimal.Round(d, 0, MidpointRounding.AwayFromZero), _ => long.TryParse(valor.ToString(), out var l2) ? l2 : 0L };
                return entero.ToString(f.ValueFormat ?? "0", CultureInfo.InvariantCulture);
            }
            default:
                return Texto(valor.ToString() ?? string.Empty, formato);
        }
    }

    private static string Mapear(BankFileFormatField f, string texto)
    {
        if (string.IsNullOrEmpty(f.ValueMapJson)) return texto;
        var mapa = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(f.ValueMapJson);
        if (mapa is null) return texto;
        foreach (var (k, v) in mapa)
            if (string.Equals(k, texto, StringComparison.OrdinalIgnoreCase)) return v;
        return texto;
    }

    private static string Ajustar(BankFileFormat formato, BankFileFormatField f, string texto, int lineNumber)
    {
        if (f.Length is not { } largo) return texto;
        if (texto.Length > largo)
        {
            if (!f.Truncate || f.DataType != BankFieldDataType.Text)
                throw new BankFileWriteException(lineNumber, f.Name, $"El campo «{f.Name}» ({texto.Length} caracteres) no cabe en {largo} posiciones.");
            texto = f.Alignment == BankFieldAlignment.Right ? texto[^largo..] : texto[..largo];
        }
        if (formato.Kind == BankFileKind.Delimited) return texto;
        var pad = string.IsNullOrEmpty(f.PadChar) ? ' ' : f.PadChar[0];
        return f.Alignment == BankFieldAlignment.Right ? texto.PadLeft(largo, pad) : texto.PadRight(largo, pad);
    }

    /// <summary>Mayúsculas y sin tildes según el formato (Ñ → N, á → A): lo que los bancos suelen exigir en ASCII.</summary>
    public static string Texto(string valor, BankFileFormat formato)
    {
        var t = valor.Trim();
        if (formato.StripAccents) t = SinTildes(t);
        if (formato.Uppercase) t = t.ToUpperInvariant();
        return t;
    }

    public static string SinTildes(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static Encoding Codificacion(string nombre)
    {
        var n = (nombre ?? "us-ascii").Trim().ToLowerInvariant();
        if (n is "us-ascii" or "ascii") return Encoding.ASCII;
        if (n == "utf-8") return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (!_codePagesRegistrados)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _codePagesRegistrados = true;
        }
        return Encoding.GetEncoding(n);
    }
}
