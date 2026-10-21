namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CierreDeCarteraRealizado</c> v1 (§10.23; <c>CIR</c>; informativo, con recibo): origen el cierre mensual (la versión), clave
/// <c>Close:{versión}</c>. Admite proceso (el cierre programado).
/// </summary>
public sealed record CierreDeCarteraRealizadoV1
{
    public const string Type = "CierreDeCarteraRealizado";

    public int Year { get; init; }

    public int Month { get; init; }

    public int CloseVersion { get; init; }

    /// <summary>Valor de <c>MonthlyCloseKind</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public DateTimeOffset InformationCutoffAt { get; init; }

    /// <summary><c>ObligatoryModelUnavailable</c>, <c>UnvalidatedParameters</c>, <c>IncompleteHistory</c>.</summary>
    public IReadOnlyList<string> Marks { get; init; } = [];

    /// <summary>Partes emitidas por tipo, con su total.</summary>
    public IReadOnlyList<FactPartSummaryV1> FactParts { get; init; } = [];
}

/// <summary>Las partes de un tipo de hecho emitidas por el cierre (§10.23, <c>factParts</c>).</summary>
public sealed record FactPartSummaryV1
{
    public string Type { get; init; } = string.Empty;

    public int Parts { get; init; }

    public decimal Total { get; init; }
}
