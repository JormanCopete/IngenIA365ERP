namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>AportesAmortizados</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>AMZ</c>; entrega K10): amortización o readquisición de aportes (FR-047), forma masiva; <c>AO</c>. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record AportesAmortizadosV1
{
    public const string Type = "AportesAmortizados";

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
}
