using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_AccountInstallmentNonDeductions</c> (feature 014, US3, T081; data-model §3.7; FR-089, FR-125): lo que Nómina o una pagaduría informó que
/// <b>no descontó</b> de una cuota de cuenta, con su motivo. La cuota sigue exigible (US3-2): esto sólo deja el motivo para la consulta, los informes
/// y la gestión. Gemela de <c>LND_InstallmentNonDeductions</c> de los créditos. Hecho de sólo inserción (<see cref="IHechoInmutable"/>), único por cuota,
/// recaudador y clave: un reintento de Nómina no lo duplica. (nuevo)
/// </summary>
public class AccountInstallmentNonDeduction : AuditableEntity, IHechoInmutable
{
    public long InstallmentId { get; init; }

    /// <summary><c>Contract</c> (Nómina por el contrato) o <c>PaymasterResult</c> (respuesta de una pagaduría).</summary>
    public NonDeductionOrigin Origin { get; init; }

    public NonDeductionReason Reason { get; init; }

    /// <summary>Lo que se pidió descontar de la cuota.</summary>
    public decimal RequestedAmount { get; init; }

    /// <summary>Lo que sí se descontó (cero si nada).</summary>
    public decimal DeductedAmount { get; init; }

    /// <summary>Quién informó (<c>NOM</c>, el código de Recaudo).</summary>
    public string CollectorCode { get; init; } = string.Empty;

    /// <summary>La clave con que lo informó (la de su consignación).</summary>
    public string ReferenceKey { get; init; } = string.Empty;

    public DateOnly OccurredOn { get; init; }

    /// <summary>El detalle en texto que trajo el recaudador, si lo trajo.</summary>
    public string? Detail { get; init; }
}
