using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;
using IngenIA365ERP.Domain.Lending.Configuration;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLineInsurances</c> (feature 013, T0210; data-model §1.6; FR-028, FR-021, FR-064): seguros de la línea. La aseguradora
/// es persona del maestro (I-1). Hija de la versión, hecho (C42); el crédito sella los suyos en su versión de condiciones. 2026-11-16 (feature 024,
/// fase 2): un cargo de la línea es un seguro o la cuota de administración (<see cref="Kind"/>); la administración lleva también un tercero (normalmente
/// la propia cooperativa), que Contabilidad no usa.
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

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 2): seguro (de fábrica) o administración. La administración es ingreso de la cooperativa: el
    /// libro la lleva en <see cref="LoanConcept.Administration"/>, la prelación en su rubro y Contabilidad en <c>AdministracionCausada</c>; siempre
    /// computa para la usura (<see cref="ComputaParaUsura(InsuranceKind, decimal?, bool)"/>). Los créditos ya desembolsados conservan lo que sellaron. (nuevo)
    /// </summary>
    public InsuranceKind Kind { get; init; } = InsuranceKind.Insurance;


    /// <summary>El seguro de una versión ya guardada (T0351).</summary>
    public static CreditLineInsurance Nuevo(int creditLineVersionId, BorradorDeSeguro s) => new()
    {
        CreditLineVersionId = creditLineVersionId,
        Code = s.Code.Trim().ToUpperInvariant(),
        Name = s.Name.Trim(),
        InsurerPersonId = s.InsurerPersonId,
        Base = s.Base,
        Rate = s.Rate,
        ChargePeriodicity = s.ChargePeriodicity,
        AccrualTiming = s.AccrualTiming,
        IsMandatory = s.IsMandatory,
        RetainedFraction = s.RetainedFraction,
        SelfInsured = s.SelfInsured,
        IsDebtorLifeInsurance = s.IsDebtorLifeInsurance,
        Kind = s.Kind,
    };

    /// <summary>
    /// ¿Computa para la usura? Sí si la cooperativa retiene parte de la prima o se autoasegura (FR-021, R36); el seguro que paga
    /// entera a la aseguradora tiene una contraprestación distinta al crédito y no computa.
    /// </summary>
    public static bool ComputaParaUsura(decimal? retainedFraction, bool selfInsured) => retainedFraction > 0m || selfInsured;

    /// <summary>
    /// 2026-11-16 (feature 024, fase 2): la administración la retiene entera la cooperativa —no hay contraprestación distinta al crédito—, así que
    /// computa siempre para la usura (art. 68 de la Ley 45 de 1990); el seguro, según <see cref="ComputaParaUsura(decimal?, bool)"/>. (nuevo)
    /// </summary>
    public static bool ComputaParaUsura(InsuranceKind kind, decimal? retainedFraction, bool selfInsured) =>
        kind == InsuranceKind.Administration || ComputaParaUsura(retainedFraction, selfInsured);

    /// <inheritdoc cref="ComputaParaUsura(InsuranceKind, decimal?, bool)"/>
    public bool Computa() => ComputaParaUsura(Kind, RetainedFraction, SelfInsured);

    /// <summary>El seguro como lo recibe el plan (con su clase: la administración va en su propia cubeta de la cuota).</summary>
    public SeguroDelPlan ParaElPlan() => new(Code, Base, Rate, ChargePeriodicity, Computa()) { Clase = Kind };
}
