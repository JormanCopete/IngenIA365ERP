using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLineRateBands</c> (feature 013, T0210; data-model §1.4; FR-020): tasas por rango de monto, plazo o segmento del
/// solicitante. Hija de la versión, hecho (C42). Exactamente una de <see cref="AnnualRate"/> y <see cref="Margin"/> según la clase
/// de tasa de la versión; gana la primera por <see cref="Order"/> que casa.
/// </summary>
public class CreditLineRateBand : AuditableEntity, IHechoInmutable
{
    public int CreditLineVersionId { get; init; }

    public int Order { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    public int? MinTermMonths { get; init; }

    public int? MaxTermMonths { get; init; }

    public DebtorKind? DebtorKind { get; init; }

    /// <summary>Nulo = indiferente; verdadero = tasa para empleados de la cooperativa (C96).</summary>
    public bool? ForEmployees { get; init; }

    public int? MinSeniorityMonths { get; init; }

    public decimal? AnnualRate { get; init; }

    public decimal? Margin { get; init; }
}
