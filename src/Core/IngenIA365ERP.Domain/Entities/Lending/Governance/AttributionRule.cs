using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_AttributionRules</c> (feature 013, T0213; data-model §3.3; FR-038, FR-094): una regla de la matriz que, si casa, exige al
/// menos <see cref="MinimumLevelId"/>. Los criterios nulos no importan. Hecho, hijo de la matriz (C42).
/// </summary>
public class AttributionRule : AuditableEntity, IHechoInmutable
{
    public int MatrixId { get; init; }

    public int Order { get; init; }

    /// <summary>Sujeto de decisión (<c>LendingDecisionSubjects</c>).</summary>
    public string Subject { get; init; } = string.Empty;

    public int? CreditLineId { get; init; }

    public string? ModalityCode { get; init; }

    public decimal? MinConsolidatedAmount { get; init; }

    public decimal? MaxConsolidatedAmount { get; init; }

    public int? MinTermMonths { get; init; }

    public int? MaxTermMonths { get; init; }

    public CollateralKind? CollateralKind { get; init; }

    /// <summary>Nulo = no importa.</summary>
    public bool? ScoreBelowMinimum { get; init; }

    /// <summary>Nulo = no importa.</summary>
    public bool? HasWarnings { get; init; }

    /// <summary>Nulo = no importa.</summary>
    public bool? PolicyException { get; init; }

    /// <summary>Novedades y condonaciones.</summary>
    public int? MinDaysPastDue { get; init; }

    public int? MaxDaysPastDue { get; init; }

    /// <summary>Concepto condonado (FR-094).</summary>
    public LoanConcept? Concept { get; init; }

    /// <summary>Nivel mínimo exigido, de la misma matriz.</summary>
    public int MinimumLevelId { get; init; }
}
