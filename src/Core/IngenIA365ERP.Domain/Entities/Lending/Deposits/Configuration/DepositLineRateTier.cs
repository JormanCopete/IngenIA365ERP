using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;

/// <summary>
/// <c>LND_DepositLineRateTiers</c> (feature 014, US1, T061; data-model §2.2; FR-020, FR-028): los tramos de tasa de una versión de captación —por
/// saldo en la vista, el contractual y el permanente; por plazo y monto en el CDAT—. Hija de la versión, hecho de sólo inserción. Exactamente una de
/// <see cref="AnnualRate"/> y <see cref="Margin"/> según la clase de tasa de la versión. Con <c>WholeBalance</c> todo el saldo va a la tasa del tramo
/// donde cae; con <c>Stepped</c>, cada porción a la del suyo; en el CDAT gana el primero por <see cref="Order"/> que casa plazo y monto.
/// </summary>
public class DepositLineRateTier : AuditableEntity, IHechoInmutable
{
    public int DepositLineVersionId { get; init; }

    public int Order { get; init; }

    public decimal? MinBalance { get; init; }

    public decimal? MaxBalance { get; init; }

    public int? MinTermDays { get; init; }

    public int? MaxTermDays { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    public decimal? AnnualRate { get; init; }

    public decimal? Margin { get; init; }

    /// <summary>El tramo de una versión ya guardada; la valida la versión entera (<see cref="ReglasDeLineaDeCaptacion"/>).</summary>
    public static DepositLineRateTier Nuevo(int depositLineVersionId, BorradorDeTramoDeCaptacion t) => new()
    {
        DepositLineVersionId = depositLineVersionId,
        Order = t.Order,
        MinBalance = t.MinBalance,
        MaxBalance = t.MaxBalance,
        MinTermDays = t.MinTermDays,
        MaxTermDays = t.MaxTermDays,
        MinAmount = t.MinAmount,
        MaxAmount = t.MaxAmount,
        AnnualRate = t.AnnualRate,
        Margin = t.Margin,
    };
}
