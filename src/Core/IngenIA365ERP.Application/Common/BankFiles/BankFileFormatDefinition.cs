using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Common.BankFiles;

/// <summary>
/// El formato de archivo bancario tal como viaja por HTTP y como lo documenta
/// <c>contracts/archivos.md</c> §2.1: una cabecera, un detalle y unos totales, cada uno con sus
/// campos. Es el espejo JSON de <see cref="BankFileFormat"/> (Core, ligado al banco, D-42). Los
/// orígenes se aceptan por su nombre genérico (<c>PayeeDocument</c>, <c>Amount</c>…) o por el
/// sinónimo de nómina del contrato (<c>EmployeeDocument</c>, <c>NetAmount</c>, <c>PaymentConcept</c>).
/// </summary>
public sealed class BankFileFormatDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? BankPublicId { get; set; }
    public string Scope { get; set; } = nameof(BankFileScope.PayrollDisbursement);
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string Kind { get; set; } = nameof(BankFileKind.FixedWidth);
    public string? Delimiter { get; set; }
    public bool QuoteText { get; set; }
    public string Encoding { get; set; } = "us-ascii";
    public string LineEnding { get; set; } = "CRLF";
    public bool Uppercase { get; set; } = true;
    public bool StripAccents { get; set; } = true;
    public string AmountFormat { get; set; } = nameof(BankFileAmountFormat.Integer);
    public string FileName { get; set; } = "PAGO{PaymentDate:yyyyMMdd}.txt";
    public string ContentType { get; set; } = "text/plain";
    public string? AgreementCode { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public BankFileRecordsDefinition Records { get; set; } = new();

    public sealed class BankFileRecordsDefinition
    {
        public BankFileRecordDefinition Header { get; set; } = new() { Enabled = true };
        public BankFileRecordDefinition Detail { get; set; } = new() { Enabled = true };
        public BankFileRecordDefinition Trailer { get; set; } = new() { Enabled = true };
    }

    public sealed class BankFileRecordDefinition
    {
        public bool Enabled { get; set; } = true;
        public List<BankFileFieldDefinition> Fields { get; set; } = [];
    }

    public sealed class BankFileFieldDefinition
    {
        public int Order { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = nameof(BankFieldSource.Constant);
        public string? Value { get; set; }
        public int? Length { get; set; }
        public string Align { get; set; } = nameof(BankFieldAlignment.Left);
        public string Pad { get; set; } = " ";
        public string? DataType { get; set; }
        public string? Format { get; set; }
        public Dictionary<string, string>? Map { get; set; }
        public bool Required { get; set; }
        public bool Truncate { get; set; } = true;
    }

    // ------------------------------------------------------------- orígenes --

    /// <summary>Sinónimos del contrato (nómina) para los orígenes genéricos del beneficiario.</summary>
    private static readonly Dictionary<string, BankFieldSource> Sinonimos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EmployeeDocumentType"] = BankFieldSource.PayeeDocumentType,
        ["EmployeeDocument"] = BankFieldSource.PayeeDocument,
        ["EmployeeFullName"] = BankFieldSource.PayeeFullName,
        ["EmployeeFirstNames"] = BankFieldSource.PayeeFirstNames,
        ["EmployeeLastNames"] = BankFieldSource.PayeeLastNames,
        ["EmployeeBankCode"] = BankFieldSource.PayeeBankCode,
        ["EmployeeAccountType"] = BankFieldSource.PayeeAccountType,
        ["EmployeeAccountNumber"] = BankFieldSource.PayeeAccountNumber,
        ["EmployeeEmail"] = BankFieldSource.PayeeEmail,
        ["EmployeeHireDate"] = BankFieldSource.PayeeHireDate,
        ["NetAmount"] = BankFieldSource.Amount,
        ["SeveranceAmount"] = BankFieldSource.Amount,
        ["PaymentConcept"] = BankFieldSource.Concept,
        ["OriginAccountNumber"] = BankFieldSource.SourceAccountNumber,
        ["OriginAccountType"] = BankFieldSource.SourceAccountType,
        ["OriginAgreementCode"] = BankFieldSource.SourceAgreementCode,
        ["CompanyTaxId"] = BankFieldSource.CompanyNit,
        ["Reference"] = BankFieldSource.BatchReference,
        ["FileDate"] = BankFieldSource.GenerationDate,
        ["LineSequence"] = BankFieldSource.LineNumber,
        ["DetailCount"] = BankFieldSource.LineCount,
    };

    public static BankFieldSource? ParseSource(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        if (Sinonimos.TryGetValue(nombre.Trim(), out var s)) return s;
        return Enum.TryParse<BankFieldSource>(nombre.Trim(), true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ------------------------------------------------------- entidad ↔ DTO --

    /// <summary>Vuelca la definición sobre la entidad (cabecera y campos); los campos anteriores se reemplazan.</summary>
    public void AplicarA(BankFileFormat formato, int? bankId)
    {
        formato.Code = Code.Trim().ToUpperInvariant();
        formato.Name = Name.Trim();
        formato.BankId = bankId;
        formato.Scope = Enum.Parse<BankFileScope>(Scope, true);
        formato.ValidFrom = ValidFrom;
        formato.ValidTo = ValidTo;
        formato.Kind = Enum.Parse<BankFileKind>(Kind, true);
        formato.Delimiter = formato.Kind == BankFileKind.Delimited ? Delimiter : null;
        formato.QuoteText = formato.Kind == BankFileKind.Delimited && QuoteText;
        formato.Encoding = Encoding.Trim().ToLowerInvariant();
        formato.LineEnding = LineEnding.Equals("LF", StringComparison.OrdinalIgnoreCase) ? BankFileLineEnding.Lf : BankFileLineEnding.Crlf;
        formato.Uppercase = Uppercase;
        formato.StripAccents = StripAccents;
        formato.AmountFormat = Enum.Parse<BankFileAmountFormat>(AmountFormat, true);
        formato.FileNamePattern = FileName.Trim();
        formato.ContentType = string.IsNullOrWhiteSpace(ContentType) ? "text/plain" : ContentType.Trim();
        formato.AgreementCode = string.IsNullOrWhiteSpace(AgreementCode) ? null : AgreementCode.Trim();
        formato.HasHeader = Records.Header.Enabled;
        formato.HasTrailer = Records.Trailer.Enabled;
        formato.IsActive = IsActive;
        formato.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();

        formato.Fields.Clear();
        foreach (var (registro, def) in new[] { (BankFileRecord.Header, Records.Header), (BankFileRecord.Detail, Records.Detail), (BankFileRecord.Trailer, Records.Trailer) })
        {
            if (!def.Enabled) continue;
            foreach (var f in def.Fields.OrderBy(f => f.Order))
            {
                var source = ParseSource(f.Source) ?? throw new InvalidOperationException($"Origen desconocido: {f.Source}");
                formato.Fields.Add(new BankFileFormatField
                {
                    Record = registro,
                    Order = f.Order,
                    Name = f.Name.Trim(),
                    Source = source,
                    ConstantValue = source == BankFieldSource.Constant ? f.Value : null,
                    Length = f.Length,
                    Alignment = f.Align.Equals("Right", StringComparison.OrdinalIgnoreCase) ? BankFieldAlignment.Right : BankFieldAlignment.Left,
                    PadChar = string.IsNullOrEmpty(f.Pad) ? " " : f.Pad[..1],
                    DataType = f.DataType is { } dt && Enum.TryParse<BankFieldDataType>(dt, true, out var tipo) ? tipo : TipoPorDefecto(source),
                    ValueFormat = string.IsNullOrWhiteSpace(f.Format) ? null : f.Format.Trim(),
                    ValueMapJson = f.Map is { Count: > 0 } ? JsonSerializer.Serialize(f.Map, Json) : null,
                    Required = f.Required,
                    Truncate = f.Truncate,
                });
            }
        }
    }

    public static BankFileFormatDefinition Desde(BankFileFormat formato)
    {
        var def = new BankFileFormatDefinition
        {
            Code = formato.Code,
            Name = formato.Name,
            BankPublicId = formato.Bank?.PublicId,
            Scope = formato.Scope.ToString(),
            ValidFrom = formato.ValidFrom,
            ValidTo = formato.ValidTo,
            Kind = formato.Kind.ToString(),
            Delimiter = formato.Delimiter,
            QuoteText = formato.QuoteText,
            Encoding = formato.Encoding,
            LineEnding = formato.LineEnding == BankFileLineEnding.Lf ? "LF" : "CRLF",
            Uppercase = formato.Uppercase,
            StripAccents = formato.StripAccents,
            AmountFormat = formato.AmountFormat.ToString(),
            FileName = formato.FileNamePattern,
            ContentType = formato.ContentType,
            AgreementCode = formato.AgreementCode,
            IsActive = formato.IsActive,
            Notes = formato.Notes,
        };
        def.Records.Header = Registro(formato, BankFileRecord.Header, formato.HasHeader);
        def.Records.Detail = Registro(formato, BankFileRecord.Detail, true);
        def.Records.Trailer = Registro(formato, BankFileRecord.Trailer, formato.HasTrailer);
        return def;
    }

    private static BankFileRecordDefinition Registro(BankFileFormat formato, BankFileRecord registro, bool habilitado) => new()
    {
        Enabled = habilitado,
        Fields = formato.Fields.Where(f => f.Record == registro && !f.IsDeleted).OrderBy(f => f.Order).Select(f => new BankFileFieldDefinition
        {
            Order = f.Order,
            Name = f.Name,
            Source = f.Source.ToString(),
            Value = f.ConstantValue,
            Length = f.Length,
            Align = f.Alignment.ToString(),
            Pad = f.PadChar,
            DataType = f.DataType.ToString(),
            Format = f.ValueFormat,
            Map = f.ValueMapJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(f.ValueMapJson, Json),
            Required = f.Required,
            Truncate = f.Truncate,
        }).ToList(),
    };

    /// <summary>El tipo de dato que el origen sugiere cuando el formato no lo dice.</summary>
    public static BankFieldDataType TipoPorDefecto(BankFieldSource source) => source switch
    {
        BankFieldSource.Amount or BankFieldSource.TotalAmount or BankFieldSource.SeveranceBaseSalary => BankFieldDataType.Amount,
        BankFieldSource.PaymentDate or BankFieldSource.GenerationDate or BankFieldSource.PayeeHireDate => BankFieldDataType.Date,
        BankFieldSource.LineNumber or BankFieldSource.LineCount or BankFieldSource.Sequence or BankFieldSource.SeveranceDays or BankFieldSource.Year => BankFieldDataType.Integer,
        _ => BankFieldDataType.Text,
    };

    public static BankFileFormatDefinition? Parse(string json) => JsonSerializer.Deserialize<BankFileFormatDefinition>(json, Json);
    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
