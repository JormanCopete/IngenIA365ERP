using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>ResultadoDeDescuentos</c> v2 (feature 014, T091; D36; mensajes.md §5.7 de la 014; <c>REC → Lending</c>): como la v1, más el grupo del archivo y lo que
/// la pagaduría informa <b>por empleado y por código suyo</b> (<c>conceptLines</c>), sin conocer las líneas de la Cartera; la Cartera lo reparte con las
/// equivalencias del convenio. <c>lines</c> de la v1 sigue admitido para las pagadurías que responden línea a línea (sólo líneas de crédito). Clave
/// <c>Confirmation:{archivo:N}</c>; no admite proceso. (nuevo)
/// </summary>
public sealed record ResultadoDeDescuentosV2
{
    public const string Type = ResultadoDeDescuentosV1.Type;

    public Guid FileMessageId { get; init; }

    public Guid FilePublicId { get; init; }

    public int FileVersion { get; init; }

    public string FileSha256 { get; init; } = string.Empty;

    public string FileGroupCode { get; init; } = string.Empty;

    public Guid PaymasterPersonPublicId { get; init; }

    public PayrollPeriodRefV1 Period { get; init; } = new();

    public DeductionResponseFileV1? ResponseFile { get; init; }

    public IReadOnlyList<DeductionConceptLineV2> ConceptLines { get; init; } = [];

    public IReadOnlyList<DeductionResultLineV1> Lines { get; init; } = [];

    public DeductionResultTotalsV1 Totals { get; init; } = new();

    public UserRefV1 ProcessedBy { get; init; } = new();
}

/// <summary>Lo que la pagaduría informa de un empleado por un código suyo (mensajes.md §5.7, <c>conceptLines</c>).</summary>
public sealed record DeductionConceptLineV2
{
    public string PayerEmployeeId { get; init; } = string.Empty;

    public Guid? PersonPublicId { get; init; }

    public string PaymasterConceptCode { get; init; } = string.Empty;

    public decimal Expected { get; init; }

    public decimal Deducted { get; init; }

    public string? PaymasterResultCode { get; init; }

    public DateOnly? DeductionDate { get; init; }
}
