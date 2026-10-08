using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_AttributionRules</c> (feature 013, T0213; data-model §3.3; FR-038, FR-094): una regla de la matriz que, si casa, exige al
/// menos <see cref="MinimumLevelId"/>. Los criterios nulos no importan. Hecho, hijo de la matriz (C42); nace sólo con la matriz publicada
/// (<see cref="MatrizPublicada.Reglas"/>, T0280).
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

    /// <summary>La regla de una matriz ya validada por <see cref="AttributionMatrix.Validar"/>, con el Id de su nivel mínimo.</summary>
    internal static AttributionRule Nueva(int matrixId, int minimumLevelId, BorradorDeRegla r) => new()
    {
        MatrixId = matrixId,
        Order = r.Order,
        Subject = r.Subject,
        CreditLineId = r.CreditLineId,
        ModalityCode = string.IsNullOrWhiteSpace(r.ModalityCode) ? null : r.ModalityCode.Trim(),
        MinConsolidatedAmount = r.MinConsolidatedAmount,
        MaxConsolidatedAmount = r.MaxConsolidatedAmount,
        MinTermMonths = r.MinTermMonths,
        MaxTermMonths = r.MaxTermMonths,
        CollateralKind = r.CollateralKind,
        ScoreBelowMinimum = r.ScoreBelowMinimum,
        HasWarnings = r.HasWarnings,
        PolicyException = r.PolicyException,
        MinDaysPastDue = r.MinDaysPastDue,
        MaxDaysPastDue = r.MaxDaysPastDue,
        Concept = r.Concept,
        MinimumLevelId = minimumLevelId,
    };
}
