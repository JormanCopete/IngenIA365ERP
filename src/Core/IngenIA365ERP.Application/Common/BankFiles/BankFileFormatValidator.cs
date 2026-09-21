using System.Text.RegularExpressions;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Common.BankFiles;

/// <summary>Un problema del formato, con el registro, el orden y el campo que lo tienen.</summary>
public sealed record BankFileFormatError(string Record, int? Order, string? Field, string Message);

/// <summary>
/// Reglas de un formato antes de guardarlo (contracts/archivos.md §2.1): órdenes consecutivos sin
/// huecos, largo en todo campo de ancho fijo, constantes con valor, mapa sólo en texto, totales
/// fuera del detalle, orígenes del beneficiario sólo en el detalle y un nombre de archivo con
/// orígenes válidos. Un formato con errores no se guarda: un archivo a medias es peor que
/// ninguno.
/// </summary>
public static class BankFileFormatValidator
{
    private static readonly HashSet<string> Codificaciones = new(StringComparer.OrdinalIgnoreCase) { "us-ascii", "ascii", "utf-8", "windows-1252", "iso-8859-1" };

    private static readonly HashSet<BankFieldSource> SoloDetalle =
    [
        BankFieldSource.LineNumber, BankFieldSource.PayeeDocumentType, BankFieldSource.PayeeDocument,
        BankFieldSource.PayeeFullName, BankFieldSource.PayeeFirstNames, BankFieldSource.PayeeLastNames,
        BankFieldSource.PayeeBankCode, BankFieldSource.PayeeAccountType, BankFieldSource.PayeeAccountNumber,
        BankFieldSource.Amount, BankFieldSource.Concept, BankFieldSource.PayeeEmail,
        BankFieldSource.SeveranceDays, BankFieldSource.SeveranceBaseSalary, BankFieldSource.PayeeHireDate,
    ];

    private static readonly HashSet<BankFieldSource> FueraDelDetalle =
    [
        BankFieldSource.Sequence, BankFieldSource.LineCount, BankFieldSource.TotalAmount,
    ];

    /// <summary>Orígenes admitidos en la plantilla del nombre del archivo.</summary>
    private static readonly HashSet<BankFieldSource> EnNombre =
    [
        BankFieldSource.PaymentDate, BankFieldSource.GenerationDate, BankFieldSource.GenerationTime,
        BankFieldSource.Sequence, BankFieldSource.BatchReference, BankFieldSource.CompanyNit, BankFieldSource.Year,
        BankFieldSource.SourceAgreementCode, BankFieldSource.LineCount,
    ];

    private static readonly Regex Token = new(@"\{(?<src>[A-Za-z]+)(?::(?<fmt>[^}]+))?\}", RegexOptions.Compiled);

    public static IReadOnlyList<BankFileFormatError> Validar(BankFileFormatDefinition def)
    {
        var errores = new List<BankFileFormatError>();
        void Error(string registro, int? orden, string? campo, string mensaje) => errores.Add(new BankFileFormatError(registro, orden, campo, mensaje));

        if (string.IsNullOrWhiteSpace(def.Code)) Error("format", null, "code", "El código es obligatorio.");
        else if (!Regex.IsMatch(def.Code.Trim(), "^[A-Z0-9][A-Z0-9_-]{0,19}$", RegexOptions.IgnoreCase)) Error("format", null, "code", "El código va en mayúsculas, sin espacios, hasta 20 caracteres (letras, números, guion).");
        if (string.IsNullOrWhiteSpace(def.Name)) Error("format", null, "name", "El nombre es obligatorio.");
        if (!Enum.TryParse<BankFileScope>(def.Scope, true, out _)) Error("format", null, "scope", $"«{def.Scope}» no es un ámbito conocido ({string.Join(", ", Enum.GetNames<BankFileScope>())}).");
        var esFijo = def.Kind.Equals(nameof(BankFileKind.FixedWidth), StringComparison.OrdinalIgnoreCase);
        var esDelimitado = def.Kind.Equals(nameof(BankFileKind.Delimited), StringComparison.OrdinalIgnoreCase);
        if (!esFijo && !esDelimitado) Error("format", null, "kind", "El tipo es FixedWidth o Delimited.");
        if (esDelimitado && string.IsNullOrEmpty(def.Delimiter)) Error("format", null, "delimiter", "Un formato delimitado necesita el separador.");
        if (!Codificaciones.Contains(def.Encoding ?? string.Empty)) Error("format", null, "encoding", "La codificación es us-ascii, utf-8 o windows-1252.");
        if (!def.LineEnding.Equals("CRLF", StringComparison.OrdinalIgnoreCase) && !def.LineEnding.Equals("LF", StringComparison.OrdinalIgnoreCase)) Error("format", null, "lineEnding", "El fin de línea es CRLF o LF.");
        if (!Enum.TryParse<BankFileAmountFormat>(def.AmountFormat, true, out _)) Error("format", null, "amountFormat", "El formato de montos es Integer, ImplicitCents o Point2.");
        if (def.ValidTo is { } hasta && hasta < def.ValidFrom) Error("format", null, "validTo", "La vigencia termina antes de empezar.");

        if (string.IsNullOrWhiteSpace(def.FileName)) Error("format", null, "fileName", "El nombre del archivo es obligatorio.");
        else
            foreach (Match m in Token.Matches(def.FileName))
            {
                var src = BankFileFormatDefinition.ParseSource(m.Groups["src"].Value);
                if (src is null || !EnNombre.Contains(src.Value))
                    Error("format", null, "fileName", $"«{m.Groups["src"].Value}» no sirve en el nombre del archivo (admite {string.Join(", ", EnNombre)}).");
            }

        if (!def.Records.Detail.Enabled || def.Records.Detail.Fields.Count == 0) Error("detail", null, null, "El detalle es obligatorio y necesita al menos un campo.");

        foreach (var (nombre, registro) in new[] { ("header", def.Records.Header), ("detail", def.Records.Detail), ("trailer", def.Records.Trailer) })
        {
            if (!registro.Enabled) continue;
            var ordenes = registro.Fields.Select(f => f.Order).OrderBy(o => o).ToList();
            for (var i = 0; i < ordenes.Count; i++)
                if (ordenes[i] != i + 1) { Error(nombre, ordenes[i], null, $"Los órdenes van 1..{ordenes.Count} sin huecos ni repetidos."); break; }

            foreach (var f in registro.Fields)
            {
                var src = BankFileFormatDefinition.ParseSource(f.Source);
                if (src is null) { Error(nombre, f.Order, f.Name, $"Origen desconocido «{f.Source}»."); continue; }
                if (string.IsNullOrWhiteSpace(f.Name)) Error(nombre, f.Order, null, "Cada campo lleva nombre.");
                if (esFijo && (f.Length is null || f.Length <= 0)) Error(nombre, f.Order, f.Name, "En ancho fijo todo campo tiene largo.");
                if (f.Length is < 0) Error(nombre, f.Order, f.Name, "El largo no puede ser negativo.");
                if (src == BankFieldSource.Constant && string.IsNullOrEmpty(f.Value)) Error(nombre, f.Order, f.Name, "Una constante necesita su valor.");
                var tipo = f.DataType is { } dt && Enum.TryParse<BankFieldDataType>(dt, true, out var t) ? t : BankFileFormatDefinition.TipoPorDefecto(src.Value);
                if (f.Map is { Count: > 0 } && tipo != BankFieldDataType.Text) Error(nombre, f.Order, f.Name, "El mapa de equivalencias sólo aplica a campos de texto.");
                if (nombre == "detail" && FueraDelDetalle.Contains(src.Value)) Error(nombre, f.Order, f.Name, $"{src} va en la cabecera o en los totales, no en el detalle.");
                if (nombre != "detail" && SoloDetalle.Contains(src.Value)) Error(nombre, f.Order, f.Name, $"{src} es del beneficiario: sólo va en el detalle.");
                if (!string.IsNullOrEmpty(f.Pad) && f.Pad.Length != 1) Error(nombre, f.Order, f.Name, "El relleno es un solo carácter (espacio o cero).");
                if (!f.Align.Equals("Left", StringComparison.OrdinalIgnoreCase) && !f.Align.Equals("Right", StringComparison.OrdinalIgnoreCase)) Error(nombre, f.Order, f.Name, "La alineación es Left o Right.");
            }
        }

        return errores;
    }
}
