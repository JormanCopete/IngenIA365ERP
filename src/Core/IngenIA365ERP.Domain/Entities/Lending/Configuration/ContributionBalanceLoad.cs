using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_ContributionBalanceLoads</c> (feature 013, T0210; data-model §2.15; D19, FR-029): saldo de aportes y de ahorro permanente
/// cargado por plantilla mientras la consulta de aportes responda «no disponible». Hecho (C42).
/// </summary>
public class ContributionBalanceLoad : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; init; }

    public DateOnly AsOfDate { get; init; }

    public decimal ContributionsBalance { get; init; }

    public decimal PermanentSavingsBalance { get; init; }

    /// <summary>Lote de la plantilla <c>lending.contribution-balances</c>.</summary>
    public Guid ImportPublicId { get; init; }

    public DateTime RegisteredAt { get; init; }

    public int RegisteredByUserId { get; init; }

    /// <summary>Fuente del saldo cargado (columna de la plantilla, C118).</summary>
    public string? Source { get; init; }
}
