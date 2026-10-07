using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLineDocumentRequirements</c> (feature 013, T0210; data-model §1.5; FR-025, FR-035): los documentos de la línea con
/// sus condiciones. Hija de la versión, hecho (C42).
/// </summary>
public class CreditLineDocumentRequirement : AuditableEntity, IHechoInmutable
{
    public int CreditLineVersionId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsMandatory { get; init; }

    /// <summary>Nulo = a todas las partes.</summary>
    public CreditPartyRole? AppliesTo { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    public DebtorKind? DebtorKind { get; init; }

    public RepaymentChannel? RepaymentChannel { get; init; }

    public int? MaxAgeDays { get; init; }

    public int Order { get; init; }
}
