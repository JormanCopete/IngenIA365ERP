namespace IngenIA365ERP.Shared.Services.Nomina;

// ============================================================================
// Feature 010 (N4, US8): dispersión bancaria y formatos de archivo plano (contracts/api.md §9).
// Los formatos son de Core y van ligados al banco (D-42): los usan nómina, tesorería y contabilidad.
// ============================================================================

/// <summary>Un formato tal como lo lista <c>GET /api/core/bank-file-formats</c>.</summary>
public sealed record FormatoBancarioResumenDto(
    Guid FormatPublicId, string Code, string Name, Guid? BankPublicId, string? BankName, string Scope,
    DateOnly ValidFrom, DateOnly? ValidTo, string Kind, bool IsActive, bool IsSeeded, bool VigenteHoy,
    int FieldCount, int FilesGenerated, string? Notes)
{
    public string AmbitoTexto => Scope switch
    {
        "PayrollDisbursement" => "Dispersión de nómina",
        "SeveranceDeposit" => "Consignación de cesantías",
        "SupplierPayments" => "Pagos a proveedores (tesorería)",
        _ => Scope,
    };
    public string BancoTexto => BankName ?? "Genérico (cualquier banco)";
    public string TipoTexto => Kind == "Delimited" ? "Delimitado" : "Ancho fijo";
    public string VigenciaTexto => ValidTo is { } h ? $"{ValidFrom:dd/MM/yyyy} – {h:dd/MM/yyyy}" : $"desde {ValidFrom:dd/MM/yyyy}";
    public string EstadoTexto => !IsActive ? "Inactivo" : VigenteHoy ? "Vigente" : "Fuera de vigencia";
}

/// <summary>La definición completa (espejo de <c>contracts/archivos.md</c> §2.1); es lo que se edita y se envía tal cual.</summary>
public sealed class FormatoBancarioDefinicion
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? BankPublicId { get; set; }
    public string Scope { get; set; } = "PayrollDisbursement";
    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? ValidTo { get; set; }
    public string Kind { get; set; } = "FixedWidth";
    public string? Delimiter { get; set; }
    public bool QuoteText { get; set; }
    public string Encoding { get; set; } = "us-ascii";
    public string LineEnding { get; set; } = "CRLF";
    public bool Uppercase { get; set; } = true;
    public bool StripAccents { get; set; } = true;
    public string AmountFormat { get; set; } = "Integer";
    public string FileName { get; set; } = "PAGO{PaymentDate:yyyyMMdd}.txt";
    public string ContentType { get; set; } = "text/plain";
    public string? AgreementCode { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public RegistrosDeFormato Records { get; set; } = new();

    public sealed class RegistrosDeFormato
    {
        public RegistroDeFormato Header { get; set; } = new() { Enabled = true };
        public RegistroDeFormato Detail { get; set; } = new() { Enabled = true };
        public RegistroDeFormato Trailer { get; set; } = new() { Enabled = true };
    }

    public sealed class RegistroDeFormato
    {
        public bool Enabled { get; set; } = true;
        public List<CampoDeFormato> Fields { get; set; } = [];
    }

    public sealed class CampoDeFormato
    {
        public int Order { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = "Constant";
        public string? Value { get; set; }
        public int? Length { get; set; }
        public string Align { get; set; } = "Left";
        public string Pad { get; set; } = " ";
        public string? DataType { get; set; }
        public string? Format { get; set; }
        public Dictionary<string, string>? Map { get; set; }
        public bool Required { get; set; }
        public bool Truncate { get; set; } = true;

        /// <summary>El mapa como texto «clave=valor;clave=valor» para editarlo en una sola caja.</summary>
        public string MapTexto
        {
            get => Map is null ? string.Empty : string.Join(";", Map.Select(kv => $"{kv.Key}={kv.Value}"));
            set
            {
                var pares = (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(p => p.Split('=', 2)).Where(p => p.Length == 2 && p[0].Trim().Length > 0)
                    .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
                Map = pares.Count == 0 ? null : pares;
            }
        }
    }
}

public sealed record FormatoBancarioDetalleDto(FormatoBancarioResumenDto Summary, FormatoBancarioDefinicion Definition);

public sealed record OrigenDeCampoDto(string Code, string Description, string DataType, bool Header, bool Detail, bool Trailer, string? Scope);

public sealed record FormatoBancarioCreadoDto(Guid FormatPublicId);

public sealed record ErrorDeFormatoDto(string Record, int? Order, string? Field, string Message);

// ------------------------------------------------------------- archivos --

public sealed record ExcluidoDeDispersionDto(Guid EmployeePublicId, string Name, string ReasonCode, string Reason, decimal NetPay)
{
    public string MotivoTexto => ReasonCode switch
    {
        "NoBankAccount" => "Sin cuenta o banco en la ficha",
        "AlreadyPaid" => "Ya pagado",
        "AlreadySent" => "Ya en otro archivo",
        "BankCodeMissing" => "Banco sin código ACH",
        "ZeroNet" => "Neto en cero",
        "RequiredFieldEmpty" => "Dato requerido vacío",
        _ => ReasonCode,
    };
}

public sealed record LineaDeDispersionDto(int LineNumber, Guid EmployeePublicId, string Name, string Document, string? BankName, string? AccountType, string? AccountNumber, decimal Amount, string RecordText, bool Paid, Guid? PaymentPublicId);

public sealed record ArchivoDeDispersionDto(
    Guid FilePublicId, Guid RunPublicId, string RunLabel, string RunKind, string Status, string FormatCode, string FormatName,
    Guid? BankPublicId, string? BankName, DateOnly PaymentDate, int Sequence, string? Reference, int LineCount, decimal TotalAmount,
    int ExcludedCount, string FileName, string FileSha256, DateTime GeneratedAt, string GeneratedBy,
    DateTime? SentAt, string? SentBy, string? BankReference, DateTime? VoidedAt, string? VoidedBy, string? VoidReason)
{
    public string EstadoTexto => Status switch { "Generated" => "Generado", "Sent" => "Enviado", "Voided" => "Anulado", _ => Status };
    public bool Generado => Status == "Generated";
    public bool Enviado => Status == "Sent";
}

public sealed record DetalleDeDispersionDto(ArchivoDeDispersionDto Summary, IReadOnlyList<LineaDeDispersionDto> Lines, IReadOnlyList<ExcluidoDeDispersionDto> Excluded, string? SourceAccountNumber, Guid? FileAttachmentPublicId);

public sealed record DispersionGeneradaDto(Guid FilePublicId, string FileName, int LineCount, decimal TotalAmount, IReadOnlyList<ExcluidoDeDispersionDto> Excluded, IReadOnlyList<AvisoCorridaDto> Warnings);

public sealed record VistaPreviaDeDispersionDto(string FileName, string ContentType, IReadOnlyList<string> Lines, int LineCount, decimal TotalAmount, IReadOnlyList<ExcluidoDeDispersionDto> Excluded, IReadOnlyList<ErrorDeFormatoDto> FormatErrors);

public sealed record DispersionEnviadaDto(Guid FilePublicId, int MarkedPaid, IReadOnlyList<Guid> AlreadyMarked, DateTime PaidAt, string Reference);

public sealed record GenerarDispersionRequest(Guid RunPublicId, Guid? FormatPublicId, DateOnly PaymentDate, Guid? SourceAccountPublicId, string? Reference, IReadOnlyList<Guid>? EmployeePublicIds);

public sealed record VistaPreviaDeDispersionRequest(Guid RunPublicId, Guid? FormatPublicId, DateOnly PaymentDate, Guid? SourceAccountPublicId, string? Reference, FormatoBancarioDefinicion? Definition);

public sealed record MarcarEnviadoRequest(DateTime SentAt, string? BankReference, DateTime? PaidAt, string? Notes);

public sealed record AnularDispersionRequest(string Reason);
