using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>DescuentosPorPagaduria</c> v1 (§11.6; <c>CAR → Receipts</c>; K2, declarado en K1): una versión del archivo de descuentos,
/// sin bytes: el adjunto por su <c>PublicId</c> y su SHA-256, sus totales y sus líneas estructuradas (C19). Clave <c>Confirmation</c>.
/// </summary>
public sealed record DescuentosPorPagaduriaV1
{
    public const string Type = "DescuentosPorPagaduria";

    public PayrollAgreementRefV1 Agreement { get; init; } = new();

    public PayrollPeriodRefV1 Period { get; init; } = new();

    public int FileVersion { get; init; }

    /// <summary>El archivo de ida (<c>BankFileScope.PayrollDeductionRequest</c>).</summary>
    public DeductionFileRefV1 File { get; init; } = new();

    /// <summary>Fecha de envío que fija el convenio.</summary>
    public DateOnly SendBy { get; init; }

    public IReadOnlyList<DeductionFileLineV1> Lines { get; init; } = [];

    public DeductionFileTotalsV1 Totals { get; init; } = new();

    /// <summary>La versión reemplazada.</summary>
    public SupersededDeductionFileV1? SupersedesFile { get; init; }

    public UserRefV1 GeneratedBy { get; init; } = new();
}

/// <summary>El convenio de libranza del archivo (§11.6, <c>agreement</c>).</summary>
public sealed record PayrollAgreementRefV1
{
    public Guid PaymasterPersonPublicId { get; init; }

    public string AgreementCode { get; init; } = string.Empty;

    public int AgreementVersion { get; init; }
}

/// <summary>El adjunto del archivo de descuentos (§11.6, <c>file</c>).</summary>
public sealed record DeductionFileRefV1
{
    public Guid AttachmentPublicId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string FormatCode { get; init; } = string.Empty;
}

/// <summary>Una línea del archivo de descuentos (§11.6, <c>lines</c>; C19).</summary>
public sealed record DeductionFileLineV1
{
    public Guid DeductionFileLinePublicId { get; init; }

    public LoanRefV1 Loan { get; init; } = new();

    public Guid DebtorPersonPublicId { get; init; }

    public string PayerEmployeeId { get; init; } = string.Empty;

    public int? InstallmentNumber { get; init; }

    public DateOnly DueDate { get; init; }

    /// <summary>Valor de <c>PayrollNoveltyKind</c>.</summary>
    public string NoveltyKind { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public decimal PastDueAmount { get; init; }
}

/// <summary>Los totales del archivo de descuentos (§11.6, <c>totals</c>).</summary>
public sealed record DeductionFileTotalsV1
{
    public int LineCount { get; init; }

    public decimal Amount { get; init; }

    public decimal PastDueAmount { get; init; }

    public IReadOnlyList<DeductionNoveltyTotalV1> ByNovelty { get; init; } = [];
}

/// <summary>Los totales de un tipo de novedad del archivo (§11.6, <c>byNovelty</c>).</summary>
public sealed record DeductionNoveltyTotalV1
{
    /// <summary>Valor de <c>PayrollNoveltyKind</c>.</summary>
    public string NoveltyKind { get; init; } = string.Empty;

    public int Lines { get; init; }

    public decimal Amount { get; init; }
}

/// <summary>La versión del archivo que una nueva reemplaza (§11.6, <c>supersedesFile</c>).</summary>
public sealed record SupersededDeductionFileV1
{
    public Guid FilePublicId { get; init; }

    public int FileVersion { get; init; }

    public Guid MessageId { get; init; }
}
