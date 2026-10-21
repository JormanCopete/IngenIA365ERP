using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLineCollateralRules</c> (feature 013, T0210; data-model §1.7; FR-026, FR-043): codeudores y garantías exigidos por
/// rango. Hija de la versión, hecho (C42); gana la primera regla que casa por <see cref="Order"/>.
/// </summary>
public class CreditLineCollateralRule : AuditableEntity, IHechoInmutable
{
    public int CreditLineVersionId { get; init; }

    public int Order { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    public int? MinTermMonths { get; init; }

    public int? MaxTermMonths { get; init; }

    public int RequiredCoDebtors { get; init; }

    /// <summary>JSON con los <see cref="CollateralKind"/> admitidos.</summary>
    public string AdmittedKinds { get; init; } = "[]";

    /// <summary>Cobertura mínima sobre el monto (1,200000 = 120 %).</summary>
    public decimal? MinimumCoverage { get; init; }

    public bool ContributionsCountAsCollateral { get; init; }

    public PromissoryNoteKind? RequiredPromissoryNoteKind { get; init; }


    /// <summary>La regla de garantía de una versión ya guardada (T0351).</summary>
    public static CreditLineCollateralRule Nueva(int creditLineVersionId, BorradorDeGarantia g) => new()
    {
        CreditLineVersionId = creditLineVersionId,
        Order = g.Order,
        MinAmount = g.MinAmount,
        MaxAmount = g.MaxAmount,
        MinTermMonths = g.MinTermMonths,
        MaxTermMonths = g.MaxTermMonths,
        RequiredCoDebtors = g.RequiredCoDebtors,
        AdmittedKinds = ListaDeEnums.Conjunto(g.AdmittedKinds),
        MinimumCoverage = g.MinimumCoverage,
        ContributionsCountAsCollateral = g.ContributionsCountAsCollateral,
        RequiredPromissoryNoteKind = g.RequiredPromissoryNoteKind,
    };
}
