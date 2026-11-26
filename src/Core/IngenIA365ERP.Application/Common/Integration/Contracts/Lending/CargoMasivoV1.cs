namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CargoMasivoRegistrado</c> v1 (2026-11-25, pedido del dueño; feature 026; <c>CAR → Accounting</c>, código corto <c>CMS</c>): nació una obligación de
/// una corrida de cargos masivos, sin solicitud ni giro. Debita el capital (regla de saldo) y acredita la contrapartida del cargo de la línea, con el
/// tercero que eligió la corrida (<see cref="CounterpartPersonPublicId"/>). La corrida sella su modo (un lote, un comprobante). Sin cuentas contables.
/// (nuevo)
/// </summary>
public sealed record CargoMasivoRegistradoV1
{
    public const string Type = "CargoMasivoRegistrado";

    public LoanRefV1 Loan { get; init; } = new();

    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>La fecha del cargo.</summary>
    public DateOnly ValueDate { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>El capital (<c>Principal</c>).</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    public string RulesVersion { get; init; } = string.Empty;

    public LoanPlanSummaryV1 Plan { get; init; } = new();

    /// <summary>La corrida (<c>LND_BulkChargeRuns.PublicId</c>).</summary>
    public Guid BulkChargeRunPublicId { get; init; }

    public string BulkChargeRunNumber { get; init; } = string.Empty;

    /// <summary>El tercero de la contrapartida: el deudor o la persona (proveedor) que eligió la corrida.</summary>
    public Guid CounterpartPersonPublicId { get; init; }
}

/// <summary>
/// <c>CargoMasivoAnulado</c> v1 (feature 026; <c>CMA</c>): la obligación de un cargo masivo se anuló sin movimientos. El espejo de
/// <see cref="CargoMasivoRegistradoV1"/>: debita la contrapartida y acredita el capital, con los mismos terceros. (nuevo)
/// </summary>
public sealed record CargoMasivoAnuladoV1
{
    public const string Type = "CargoMasivoAnulado";

    public LoanRefV1 Loan { get; init; } = new();

    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>La fecha de la anulación.</summary>
    public DateOnly ValueDate { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>El capital anulado (<c>Principal</c>), positivo.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El <c>CargoMasivoRegistrado</c> que anula.</summary>
    public OriginalFactRefV1? Original { get; init; }

    public string RulesVersion { get; init; } = string.Empty;

    public Guid BulkChargeRunPublicId { get; init; }

    public string BulkChargeRunNumber { get; init; } = string.Empty;

    public Guid CounterpartPersonPublicId { get; init; }

    public string Reason { get; init; } = string.Empty;
}
