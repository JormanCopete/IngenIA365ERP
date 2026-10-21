using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;

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


    /// <summary>La banda de una versión ya guardada (T0351); la valida la versión entera (<c>CreditLineVersion.Validar</c>).</summary>
    public static CreditLineRateBand Nueva(int creditLineVersionId, BorradorDeBanda b) => new()
    {
        CreditLineVersionId = creditLineVersionId,
        Order = b.Order,
        MinAmount = b.MinAmount,
        MaxAmount = b.MaxAmount,
        MinTermMonths = b.MinTermMonths,
        MaxTermMonths = b.MaxTermMonths,
        DebtorKind = b.DebtorKind,
        ForEmployees = b.ForEmployees,
        MinSeniorityMonths = b.MinSeniorityMonths,
        AnnualRate = b.AnnualRate,
        Margin = b.Margin,
    };

    /// <summary>¿Casa la banda con la solicitud? (monto, plazo en meses, tipo de deudor, empleado, antigüedad). Gana la primera por orden.</summary>
    public bool Casa(decimal monto, int plazoMeses, DebtorKind? tipoDeDeudor, bool esEmpleado, int? antiguedadMeses) =>
        (MinAmount is null || monto >= MinAmount) && (MaxAmount is null || monto <= MaxAmount)
        && (MinTermMonths is null || plazoMeses >= MinTermMonths) && (MaxTermMonths is null || plazoMeses <= MaxTermMonths)
        && (DebtorKind is null || DebtorKind == tipoDeDeudor)
        && (ForEmployees is null || ForEmployees == esEmpleado)
        && (MinSeniorityMonths is null || (antiguedadMeses ?? 0) >= MinSeniorityMonths);
}
