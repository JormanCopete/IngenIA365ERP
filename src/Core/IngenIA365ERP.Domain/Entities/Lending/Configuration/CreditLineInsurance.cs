using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLineInsurances</c> (feature 013, T0210; data-model §1.6; FR-028, FR-021, FR-064): seguros de la línea. La aseguradora
/// es persona del maestro (I-1). Hija de la versión, hecho (C42); el crédito sella los suyos en su versión de condiciones.
/// </summary>
public class CreditLineInsurance : AuditableEntity, IHechoInmutable
{
    public int CreditLineVersionId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int InsurerPersonId { get; init; }

    public InsuranceBase Base { get; init; }

    /// <summary>Tarifa por período de cobro, como fracción de la base.</summary>
    public decimal Rate { get; init; }

    public RepaymentPeriodicity ChargePeriodicity { get; init; }

    public InsuranceAccrualTiming AccrualTiming { get; init; } = InsuranceAccrualTiming.AtDueDate;

    public bool IsMandatory { get; init; }

    /// <summary>Parte de la prima que retiene la cooperativa: si es mayor que cero, computa para usura (FR-021).</summary>
    public decimal? RetainedFraction { get; init; }

    public bool SelfInsured { get; init; }

    /// <summary>El seguro que abre la reclamación por fallecimiento (FR-097, T102).</summary>
    public bool IsDebtorLifeInsurance { get; init; }
}
