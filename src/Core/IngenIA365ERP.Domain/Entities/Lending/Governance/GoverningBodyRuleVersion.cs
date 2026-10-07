using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodyRuleVersions</c> (feature 013, T0213; data-model §3.6; C44; FR-039, FR-040, F7, T42): quórum y mayorías del
/// órgano con vigencia, en fracciones exactas (numerador/denominador) para que <c>EvaluadorDeVotacion</c> compare en enteros. Hecho
/// de sólo inserción (C42), leído con <see cref="VigenciaBitemporal"/>.
/// </summary>
public class GoverningBodyRuleVersion : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public int GoverningBodyId { get; init; }

    public int VersionNumber { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Quórum sobre la composición vigente.</summary>
    public int QuorumNumerator { get; init; }

    public int QuorumDenominator { get; init; }

    public FractionComparison QuorumComparison { get; init; }

    /// <summary>Mayoría ordinaria.</summary>
    public int MajorityNumerator { get; init; }

    public int MajorityDenominator { get; init; }

    public FractionComparison MajorityComparison { get; init; }

    public MajorityBase MajorityBase { get; init; }

    /// <summary>Sólo consejo: mayoría calificada con vinculados, sobre la composición; con actividad financiera, ≥ el límite nacional.</summary>
    public int? RelatedPartyNumerator { get; init; }

    public int? RelatedPartyDenominator { get; init; }

    /// <summary>Estatutos o reglamento.</summary>
    public string Source { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}
