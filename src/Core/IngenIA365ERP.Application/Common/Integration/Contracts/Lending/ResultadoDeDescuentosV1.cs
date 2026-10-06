using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>ResultadoDeDescuentos</c> v1 (§12.6; <c>REC → Lending</c>; K2): el resultado de la pagaduría, línea por línea, procesado por
/// Recaudo con el formato de vuelta del convenio. Clave <c>Confirmation:{archivo:N}</c>; no admite proceso.
/// </summary>
public sealed record ResultadoDeDescuentosV1
{
    public const string Type = "ResultadoDeDescuentos";

    public Guid FileMessageId { get; init; }

    public Guid FilePublicId { get; init; }

    public int FileVersion { get; init; }

    public string FileSha256 { get; init; } = string.Empty;

    public Guid PaymasterPersonPublicId { get; init; }

    public PayrollPeriodRefV1 Period { get; init; } = new();

    /// <summary>El archivo de la pagaduría tal como lo recibió Recaudo.</summary>
    public DeductionResponseFileV1? ResponseFile { get; init; }

    public IReadOnlyList<DeductionResultLineV1> Lines { get; init; } = [];

    public DeductionResultTotalsV1 Totals { get; init; } = new();

    public UserRefV1 ProcessedBy { get; init; } = new();
}

/// <summary>El archivo de respuesta de la pagaduría (§12.6, <c>responseFile</c>).</summary>
public sealed record DeductionResponseFileV1
{
    public Guid AttachmentPublicId { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}

/// <summary>El resultado de una línea del archivo de descuentos (§12.6, <c>lines</c>).</summary>
public sealed record DeductionResultLineV1
{
    public Guid DeductionFileLinePublicId { get; init; }

    public LoanRefV1 Loan { get; init; } = new();

    public int? InstallmentNumber { get; init; }

    public decimal ExpectedAmount { get; init; }

    public decimal DeductedAmount { get; init; }

    public DateOnly? DeductionDate { get; init; }

    /// <summary><c>Deducted</c>, <c>PartiallyDeducted</c> o <c>NotApplied</c>.</summary>
    public string Outcome { get; init; } = string.Empty;

    public string? PaymasterResultCode { get; init; }

    /// <summary>Valor de <c>MotivoDeNoDescuento</c>.</summary>
    public string? Reason { get; init; }

    public string? Detail { get; init; }
}

/// <summary>Los totales del resultado de descuentos (§12.6, <c>totals</c>).</summary>
public sealed record DeductionResultTotalsV1
{
    public int LineCount { get; init; }

    public decimal Expected { get; init; }

    public decimal Deducted { get; init; }

    public decimal NotApplied { get; init; }
}
