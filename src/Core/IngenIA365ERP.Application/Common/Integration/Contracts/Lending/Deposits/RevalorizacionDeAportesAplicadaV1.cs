namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>RevalorizacionDeAportesAplicada</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>RVA</c>; entrega K10): aplicación de la revalorización decidida por la asamblea (FR-045), forma masiva; <c>AO</c>. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record RevalorizacionDeAportesAplicadaV1
{
    public const string Type = "RevalorizacionDeAportesAplicada";

    /// <summary>La decisión de la asamblea que se aplica.</summary>
    public AssemblyDistributionRefV1 Distribution { get; init; } = new();

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<DepositFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por asociado.</summary>
    public IReadOnlyList<DepositFactLineV1> Lines { get; init; } = [];

    /// <summary>El destino del excedente: <c>ContributionCapital</c>, <c>DepositAccount</c> u <c>Order</c>.</summary>
    public string? Destination { get; init; }

    public int LineCount { get; init; }

    public decimal ControlTotal { get; init; }

    /// <summary>Respuesta 89 B del dueño (E260): la retención practicada a cada asociado; las líneas llevan lo neto. Vacío: no se retuvo.</summary>
    public IReadOnlyList<AssemblyWithholdingV1> Withholdings { get; init; } = [];
}

/// <summary>La decisión de la asamblea que aplica una forma masiva de aportes (mensajes.md §4.1, <c>distribution</c>).</summary>
public sealed record AssemblyDistributionRefV1
{
    public Guid PublicId { get; init; }

    /// <summary>Valor de <c>AssemblyDistributionKind</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public int Year { get; init; }

    public Guid SessionItemPublicId { get; init; }

    public string Fund { get; init; } = string.Empty;

    public decimal? Percent { get; init; }

    public decimal? Value { get; init; }

    public decimal Base { get; init; }
}
