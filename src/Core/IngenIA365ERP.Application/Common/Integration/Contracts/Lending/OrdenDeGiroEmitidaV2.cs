using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>OrdenDeGiroEmitida</c> v2 (feature 014, T055; contracts/mensajes.md §5.3 de la 014, research C11; <c>CAR → Treasury</c>): la misma orden de los
/// desembolsos con su tipo (<see cref="OrderKind"/>) y la cuenta de origen del dinero; el crédito deja de ser obligatorio. Los desembolsos de crédito
/// siguen emitiendo v1 mientras Tesorería no adopte la v2; las órdenes de captaciones salen <b>sólo</b> en v2 y la activación de aportes o captaciones en
/// producción exige un destino <c>Treasury</c> con la v2 adoptada. Se declara en la fase fundacional como contrato; la emite US10. <see cref="Account"/>
/// es la cuenta del asociado (<see cref="MemberAccountRefV1"/>), no una cuenta del plan.
/// </summary>
public sealed record OrdenDeGiroEmitidaV2
{
    public const string Type = OrdenDeGiroEmitidaV1.Type;

    public Guid OrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>
    /// <c>LoanDisbursement</c>, <c>DepositWithdrawal</c>, <c>MembershipRefund</c>, <c>ContributionRefund</c>, <c>DeceasedDelivery</c>,
    /// <c>TermDepositInterest</c>, <c>TermDepositRedemption</c>, <c>ContractualMaturity</c>, <c>UnclaimedPayment</c>, <c>GarnishmentConsignment</c> o
    /// <c>SurplusPayment</c>; también el motivo del puente de devoluciones en la matriz.
    /// </summary>
    public string OrderKind { get; init; } = string.Empty;

    /// <summary>Sólo en <c>LoanDisbursement</c>.</summary>
    public LoanRefV1? Loan { get; init; }

    /// <summary>Sólo en <c>LoanDisbursement</c>.</summary>
    public int? TrancheNumber { get; init; }

    /// <summary>La cuenta de origen del dinero.</summary>
    public MemberAccountRefV1? Account { get; init; }

    /// <summary>El retiro, el CDAT, la devolución, la entrega o el embargo que la origina.</summary>
    public Guid? SourceDocumentPublicId { get; init; }

    /// <summary><c>Transfer</c>, <c>Check</c> o <c>ThirdPartyPayment</c>.</summary>
    public string Form { get; init; } = string.Empty;

    public PayeeV1 Payee { get; init; } = new();

    public decimal GrossAmount { get; init; }

    public IReadOnlyList<PayoffDeductionV1> PayoffDeductions { get; init; } = [];

    public decimal NetAmount { get; init; }

    public DateOnly PayDate { get; init; }

    /// <summary>El número de la cuenta o del crédito.</summary>
    public string Concept { get; init; } = string.Empty;

    public bool PaidByFile { get; init; }

    public Guid? ReplacesOrderPublicId { get; init; }
}
