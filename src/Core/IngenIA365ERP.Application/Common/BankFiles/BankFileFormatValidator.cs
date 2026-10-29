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
        BankFieldSource.LoanNumber,
        // Libranza (K2): cada uno es de la línea (la obligación), no del archivo.
        BankFieldSource.PayerEmployeeId, BankFieldSource.InstallmentNumber, BankFieldSource.DueDate, BankFieldSource.NoveltyKind,
        BankFieldSource.PastDueAmount, BankFieldSource.PeriodYear, BankFieldSource.PeriodMonth, BankFieldSource.LibranzaDate,
        BankFieldSource.ResultCode, BankFieldSource.DeductedAmount, BankFieldSource.DeductionDate,
    ];

    /// <summary>
    /// Los orígenes que sirven en cualquier ámbito (feature 013, contracts/adaptadores-y-archivos.md §6.1): empresa, cuenta de origen, fechas,
    /// lote y conteos.
    /// </summary>
    private static readonly HashSet<BankFieldSource> DeTodoRegistro =
    [
        BankFieldSource.Constant, BankFieldSource.Blank, BankFieldSource.CompanyNit, BankFieldSource.CompanyNitDv, BankFieldSource.CompanyName,
        BankFieldSource.SourceAccountNumber, BankFieldSource.SourceAccountType, BankFieldSource.SourceBankCode, BankFieldSource.SourceAgreementCode,
        BankFieldSource.PaymentDate, BankFieldSource.GenerationDate, BankFieldSource.GenerationTime, BankFieldSource.Sequence,
        BankFieldSource.BatchReference, BankFieldSource.LineNumber, BankFieldSource.LineCount, BankFieldSource.TotalAmount,
    ];

    /// <summary>
    /// Feature 013 (T0452; §6.1): los orígenes del detalle admitidos por ámbito, además de <see cref="DeTodoRegistro"/>. Un ámbito que no está
    /// aquí (los de nómina y tesorería de la 010) no restringe: sigue como estaba. Un origen fuera de su ámbito es error al guardar el formato.
    /// </summary>
    private static readonly IReadOnlyDictionary<BankFileScope, HashSet<BankFieldSource>> DetallePorAmbito = new Dictionary<BankFileScope, HashSet<BankFieldSource>>
    {
        [BankFileScope.LoanDisbursement] =
        [
            BankFieldSource.PayeeDocumentType, BankFieldSource.PayeeDocument, BankFieldSource.PayeeFullName, BankFieldSource.PayeeFirstNames,
            BankFieldSource.PayeeLastNames, BankFieldSource.PayeeBankCode, BankFieldSource.PayeeAccountType, BankFieldSource.PayeeAccountNumber,
            BankFieldSource.PayeeEmail, BankFieldSource.Amount, BankFieldSource.Concept, BankFieldSource.LoanNumber,
        ],
        // K2 (T0831; §6.1): el archivo de descuentos que va a la pagaduría y la respuesta que vuelve.
        [BankFileScope.PayrollDeductionRequest] =
        [
            BankFieldSource.PayeeDocumentType, BankFieldSource.PayeeDocument, BankFieldSource.PayeeFullName, BankFieldSource.PayeeFirstNames,
            BankFieldSource.PayeeLastNames, BankFieldSource.PayerEmployeeId, BankFieldSource.LoanNumber, BankFieldSource.InstallmentNumber,
            BankFieldSource.DueDate, BankFieldSource.Amount, BankFieldSource.PastDueAmount, BankFieldSource.NoveltyKind, BankFieldSource.PeriodYear,
            BankFieldSource.PeriodMonth, BankFieldSource.LibranzaDate,
            // Feature 014 (T014, T091; decisión del dueño del 2026-10-18, D36): las cuotas de cuentas y el código de la pagaduría del archivo de un grupo.
            BankFieldSource.AccountNumber, BankFieldSource.AccountInstallmentPeriod, BankFieldSource.PaymasterConceptCode,
        ],
        [BankFileScope.PayrollDeductionResult] =
        [
            BankFieldSource.PayeeDocument, BankFieldSource.PayerEmployeeId, BankFieldSource.LoanNumber, BankFieldSource.InstallmentNumber,
            BankFieldSource.ResultCode, BankFieldSource.DeductedAmount, BankFieldSource.DeductionDate, BankFieldSource.PeriodYear,
            BankFieldSource.PeriodMonth,
            // Feature 014 (T091): la respuesta por empleado y por código de la pagaduría.
            BankFieldSource.AccountNumber, BankFieldSource.PaymasterConceptCode,
        ],
        // Feature 015 (T0023; archivos-y-plantillas.md §2, §3): el archivo de pago de un lote con el contexto genérico, el extracto y la
        // respuesta del banco. El número de cuenta de la respuesta es la del beneficiario (no la de Cartera): por eso se admite aquí.
        [BankFileScope.TreasuryPayments] =
        [
            BankFieldSource.PayeeDocumentType, BankFieldSource.PayeeDocument, BankFieldSource.PayeeFullName, BankFieldSource.PayeeFirstNames,
            BankFieldSource.PayeeLastNames, BankFieldSource.PayeeBankCode, BankFieldSource.PayeeAccountType, BankFieldSource.PayeeAccountNumber,
            BankFieldSource.PayeeEmail, BankFieldSource.Amount, BankFieldSource.Concept, BankFieldSource.BatchReference,
        ],
        [BankFileScope.BankStatement] =
        [
            BankFieldSource.MovementDate, BankFieldSource.ValueDate, BankFieldSource.Direction, BankFieldSource.Amount, BankFieldSource.Debit,
            BankFieldSource.Credit, BankFieldSource.MovementReference, BankFieldSource.BankSequence, BankFieldSource.DepositorTaxId,
            BankFieldSource.DepositorName, BankFieldSource.Description, BankFieldSource.Balance, BankFieldSource.OfficeCode,
        ],
        [BankFileScope.TreasuryPaymentResponse] =
        [
            BankFieldSource.BeneficiaryTaxId, BankFieldSource.BankCode, BankFieldSource.AccountNumber, BankFieldSource.Amount,
            BankFieldSource.Outcome, BankFieldSource.BankCause, BankFieldSource.BankDate, BankFieldSource.BankReference,
        ],
        // R2 (fase 36, T0426; archivos-y-plantillas.md §1): el archivo diario de un convenio de recaudo y el de una pasarela o PSE, de lectura. La referencia de pago va
        // en MovementReference, el identificador del pago en BankSequence y el resultado de la pasarela (aprobado o rechazado) en Outcome.
        [BankFileScope.CollectionAgreementFile] =
        [
            BankFieldSource.MovementDate, BankFieldSource.ValueDate, BankFieldSource.Amount, BankFieldSource.MovementReference, BankFieldSource.BankSequence,
            BankFieldSource.DepositorTaxId, BankFieldSource.DepositorName, BankFieldSource.Description, BankFieldSource.OfficeCode,
        ],
        [BankFileScope.PaymentGatewayFile] =
        [
            BankFieldSource.MovementDate, BankFieldSource.ValueDate, BankFieldSource.Amount, BankFieldSource.MovementReference, BankFieldSource.BankSequence,
            BankFieldSource.DepositorTaxId, BankFieldSource.DepositorName, BankFieldSource.Description, BankFieldSource.Outcome, BankFieldSource.BankReference,
            BankFieldSource.BankDate,
        ],
        // T2 (fase 44, T0464; FR-133): la liquidación de una red de tarjetas, de lectura: por lote de datáfono, el bruto, la comisión con su IVA, las retenciones y lo
        // abonado.
        [BankFileScope.AcquirerSettlement] =
        [
            BankFieldSource.MovementDate, BankFieldSource.PaymentDate, BankFieldSource.TerminalCode, BankFieldSource.TerminalBatchNumber, BankFieldSource.GrossAmount,
            BankFieldSource.CommissionAmount, BankFieldSource.CommissionVatAmount, BankFieldSource.WithholdingTaxAmount, BankFieldSource.WithholdingVatAmount,
            BankFieldSource.WithholdingIcaAmount, BankFieldSource.SettledAmount, BankFieldSource.Description,
        ],
    };

    /// <summary>
    /// Feature 015 (T0023): los orígenes que sólo leen el extracto y la respuesta de pagos: fuera de esos ámbitos son error al guardar el
    /// formato (un escritor los escribiría vacíos).
    /// </summary>
    private static readonly HashSet<BankFieldSource> SoloDeLectura =
    [
        BankFieldSource.MovementDate, BankFieldSource.ValueDate, BankFieldSource.Direction, BankFieldSource.Debit, BankFieldSource.Credit,
        BankFieldSource.BankSequence, BankFieldSource.DepositorTaxId, BankFieldSource.DepositorName, BankFieldSource.Description,
        BankFieldSource.Balance, BankFieldSource.OfficeCode, BankFieldSource.BeneficiaryTaxId, BankFieldSource.BankCode,
        BankFieldSource.Outcome, BankFieldSource.BankCause, BankFieldSource.BankDate, BankFieldSource.BankReference,
        // T2 (fase 44): los de la liquidación de un adquirente.
        BankFieldSource.TerminalCode, BankFieldSource.TerminalBatchNumber, BankFieldSource.GrossAmount, BankFieldSource.CommissionAmount,
        BankFieldSource.CommissionVatAmount, BankFieldSource.WithholdingTaxAmount, BankFieldSource.WithholdingVatAmount, BankFieldSource.WithholdingIcaAmount,
        BankFieldSource.SettledAmount,
    ];

    /// <summary>
    /// Los orígenes que sólo resuelve Cartera (feature 013, K1 y K2): fuera de los ámbitos que los admiten —también en los de nómina y
    /// tesorería, que no restringen lo demás— son error al guardar el formato. Si no, un formato de dispersión con <c>LoanNumber</c> se
    /// guardaba y escribía el campo vacío.
    /// </summary>
    private static readonly HashSet<BankFieldSource> SoloDeCartera =
    [
        BankFieldSource.LoanNumber, BankFieldSource.PayerEmployeeId, BankFieldSource.InstallmentNumber, BankFieldSource.DueDate,
        BankFieldSource.NoveltyKind, BankFieldSource.PastDueAmount, BankFieldSource.PeriodYear, BankFieldSource.PeriodMonth,
        BankFieldSource.LibranzaDate, BankFieldSource.ResultCode, BankFieldSource.DeductedAmount, BankFieldSource.DeductionDate,
        BankFieldSource.AccountNumber, BankFieldSource.AccountInstallmentPeriod, BankFieldSource.PaymasterConceptCode,
    ];

    /// <summary>Los orígenes que admite <paramref name="ambito"/>, o nulo si el ámbito no restringe (feature 013, §6.1).</summary>
    public static IReadOnlySet<BankFieldSource>? OrigenesAdmitidos(BankFileScope ambito) =>
        DetallePorAmbito.TryGetValue(ambito, out var detalle) ? DeTodoRegistro.Union(detalle).ToHashSet() : null;

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
        var admitidos = Enum.TryParse<BankFileScope>(def.Scope, true, out var ambito) ? OrigenesAdmitidos(ambito) : null;
        if (!Enum.TryParse<BankFileScope>(def.Scope, true, out _)) Error("format", null, "scope", $"«{def.Scope}» no es un ámbito conocido ({string.Join(", ", Enum.GetNames<BankFileScope>())}).");
        else if (SeleccionPorReferenciaAttribute.Tiene(ambito) && def.BankPublicId is not null)
            Error("format", null, "bankPublicId", $"Un formato de «{ambito}» es genérico: no va ligado a un banco (cada convenio lo elige por referencia).");
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
                if (admitidos is not null && !admitidos.Contains(src.Value))
                    Error(nombre, f.Order, f.Name, $"{src} no es un origen del ámbito {ambito} (admite {string.Join(", ", admitidos.Order())}).");
                else if (admitidos is null && SoloDeCartera.Contains(src.Value))
                    Error(nombre, f.Order, f.Name, $"{src} es un origen de Cartera: no sirve en el ámbito {def.Scope}.");
                else if (admitidos is null && SoloDeLectura.Contains(src.Value))
                    Error(nombre, f.Order, f.Name, $"{src} es un origen de lectura del extracto o de la respuesta de pagos: no sirve en el ámbito {def.Scope}.");
                if (string.IsNullOrWhiteSpace(f.Name)) Error(nombre, f.Order, null, "Cada campo lleva nombre.");
                if (esFijo && (f.Length is null || f.Length <= 0)) Error(nombre, f.Order, f.Name, "En ancho fijo todo campo tiene largo.");
                if (f.Length is < 0) Error(nombre, f.Order, f.Name, "El largo no puede ser negativo.");
                if (src == BankFieldSource.Constant && string.IsNullOrEmpty(f.Value)) Error(nombre, f.Order, f.Name, "Una constante necesita su valor.");
                var tipo = f.DataType is { } dt && Enum.TryParse<BankFieldDataType>(dt, true, out var t) ? t : BankFileFormatDefinition.TipoPorDefecto(src.Value);
                if (f.Map is { Count: > 0 } && tipo != BankFieldDataType.Text) Error(nombre, f.Order, f.Name, "El mapa de equivalencias sólo aplica a campos de texto.");
                if (nombre == "detail" && FueraDelDetalle.Contains(src.Value)) Error(nombre, f.Order, f.Name, $"{src} va en la cabecera o en los totales, no en el detalle.");
                if (nombre != "detail" && SoloDetalle.Contains(src.Value)) Error(nombre, f.Order, f.Name, $"{src} es de cada línea: sólo va en el detalle.");
                if (!string.IsNullOrEmpty(f.Pad) && f.Pad.Length != 1) Error(nombre, f.Order, f.Name, "El relleno es un solo carácter (espacio o cero).");
                if (!f.Align.Equals("Left", StringComparison.OrdinalIgnoreCase) && !f.Align.Equals("Right", StringComparison.OrdinalIgnoreCase)) Error(nombre, f.Order, f.Name, "La alineación es Left o Right.");
            }
        }

        return errores;
    }
}
